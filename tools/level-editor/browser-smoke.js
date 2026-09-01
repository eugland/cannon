"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const fsPromises = require("node:fs/promises");
const net = require("node:net");
const os = require("node:os");
const path = require("node:path");
const { spawn } = require("node:child_process");
const { createServer } = require("./server");

function delay(milliseconds) {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function freePort() {
  const server = net.createServer();
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  const port = server.address().port;
  await new Promise(resolve => server.close(resolve));
  return port;
}

async function waitForPage(debugPort, editorPort) {
  for (let attempt = 0; attempt < 50; attempt++) {
    try {
      const pages = await (await fetch(`http://127.0.0.1:${debugPort}/json/list`)).json();
      const page = pages.find(item => item.type === "page" && item.url.includes(`:${editorPort}`));
      if (page) return page;
    } catch {}
    await delay(100);
  }
  throw new Error("Chrome DevTools page did not become ready.");
}

class DevToolsClient {
  constructor(url) {
    this.nextId = 1;
    this.pending = new Map();
    this.socket = new WebSocket(url);
    this.ready = new Promise((resolve, reject) => {
      this.socket.addEventListener("open", resolve, { once: true });
      this.socket.addEventListener("error", reject, { once: true });
    });
    this.socket.addEventListener("message", event => {
      const message = JSON.parse(event.data);
      if (!message.id || !this.pending.has(message.id)) return;
      const { resolve, reject } = this.pending.get(message.id);
      this.pending.delete(message.id);
      if (message.error) reject(new Error(message.error.message));
      else resolve(message.result);
    });
  }

  async send(method, params = {}) {
    await this.ready;
    const id = this.nextId++;
    const response = new Promise((resolve, reject) => this.pending.set(id, { resolve, reject }));
    this.socket.send(JSON.stringify({ id, method, params }));
    return response;
  }

  async evaluate(expression) {
    const result = await this.send("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
    return result.result.value;
  }

  close() {
    this.socket.close();
  }
}

async function main() {
  const editorPort = await freePort();
  const debugPort = await freePort();
  const profile = await fsPromises.mkdtemp(path.join(os.tmpdir(), "cannon-editor-chrome-"));
  const editor = createServer();
  await new Promise(resolve => editor.listen(editorPort, "127.0.0.1", resolve));

  const chromePath = process.env.CHROME_PATH ||
    (process.platform === "win32"
      ? "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe"
      : "google-chrome");
  assert.ok(process.platform !== "win32" || fs.existsSync(chromePath), `Chrome not found: ${chromePath}`);
  const chrome = spawn(chromePath, [
    "--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run",
    `--remote-debugging-port=${debugPort}`, `--user-data-dir=${profile}`, "--window-size=1600,1000",
    `http://127.0.0.1:${editorPort}`
  ], { stdio: "ignore", windowsHide: true });
  const chromeExit = new Promise(resolve => chrome.once("exit", resolve));

  let client;
  try {
    const page = await waitForPage(debugPort, editorPort);
    client = new DevToolsClient(page.webSocketDebuggerUrl);
    await client.ready;
    for (let attempt = 0; attempt < 50; attempt++) {
      const loaded = await client.evaluate("document.querySelector('#status')?.textContent.startsWith('Loaded')");
      if (loaded) break;
      await delay(100);
    }

    const initial = await client.evaluate(`(() => {
      const rect = document.querySelector('#stage').getBoundingClientRect();
      return { left: rect.left, top: rect.top, width: rect.width, height: rect.height,
        pageHeight: document.body.scrollHeight, viewportHeight: document.body.clientHeight };
    })()`);
    const x = initial.left + initial.width / 2;
    const y = initial.top + initial.height / 2;
    await client.send("Input.dispatchMouseEvent", { type: "mousePressed", x, y, button: "left", clickCount: 1 });
    await client.send("Input.dispatchMouseEvent", { type: "mouseMoved", x: x + 32, y, button: "left", buttons: 1 });
    await client.send("Input.dispatchMouseEvent", { type: "mouseReleased", x: x + 32, y, button: "left", clickCount: 1 });
    await delay(100);

    const selected = await client.evaluate(`(() => {
      const rect = document.querySelector('#stage').getBoundingClientRect();
      return { width: rect.width, height: rect.height,
        inspector: document.querySelector('#objectInspector').textContent,
        status: document.querySelector('#status').textContent,
        pageHeight: document.body.scrollHeight, viewportHeight: document.body.clientHeight };
    })()`);
    assert.equal(selected.width, initial.width, "canvas width changed after selection");
    assert.equal(selected.height, initial.height, "canvas height changed after selection");
    assert.equal(selected.pageHeight, selected.viewportHeight, "selection grew page and distorted canvas");
    assert.match(selected.inspector, /Object instance/);
    assert.equal(selected.status, "Unsaved changes");

    const assetDialog = await client.evaluate(`(() => {
      document.querySelector('#createAssetFromObject').click();
      const form = document.querySelector('#definitionForm');
      return { open: document.querySelector('#definitionDialog').open,
        id: form.elements.id.value, kind: form.elements.kind.value };
    })()`);
    assert.equal(assetDialog.open, true);
    assert.match(assetDialog.id, /-asset/);
    assert.equal(assetDialog.kind, "planet");
    await client.evaluate("document.querySelector('#definitionForm button[value=cancel]').click()");

    await client.evaluate("window.confirm = () => true; document.querySelector('#resetCatalog').click()");
    assert.equal(await client.evaluate("document.querySelector('#status').textContent"), "Unsaved changes reset.");
    console.log("Browser smoke passed: stable canvas, object selection, drag, reset.");
  } finally {
    if (client) client.close();
    chrome.kill();
    await Promise.race([chromeExit, delay(2000)]);
    await new Promise(resolve => editor.close(resolve));
    await fsPromises.rm(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 100 });
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
