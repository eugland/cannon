"use strict";

const http = require("node:http");
const fs = require("node:fs/promises");
const path = require("node:path");

const REPO_ROOT = path.resolve(__dirname, "../..");
const DEFAULT_PATHS = {
  levels: path.join(REPO_ROOT, "Assets/Resources/LevelEditor/levels.json"),
  definitions: path.join(REPO_ROOT, "Assets/Resources/LevelEditor/objects.json"),
  publicDir: path.join(__dirname, "public")
};
const KINDS = new Set(["planet", "sun", "blackHole", "moon", "cannon", "target", "block", "explosiveBlock"]);
const MAX_BODY_BYTES = 2 * 1024 * 1024;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function assertId(value, label) {
  assert(typeof value === "string" && /^[a-z0-9][a-z0-9-]*$/i.test(value), `${label} must be a simple ID.`);
}

function assertFinite(value, label, minimum = -100000, maximum = 100000) {
  assert(Number.isFinite(value) && value >= minimum && value <= maximum,
    `${label} must be a finite number from ${minimum} to ${maximum}.`);
}

function uniqueIds(items, label) {
  const ids = new Set();
  for (const item of items) {
    assertId(item.id, `${label} ID`);
    assert(!ids.has(item.id), `Duplicate ${label} ID '${item.id}'.`);
    ids.add(item.id);
  }
}

function validateCatalog(catalog) {
  assert(catalog && Array.isArray(catalog.levels), "Catalog must contain levels array.");
  assert(Array.isArray(catalog.definitions), "Catalog must contain definitions array.");
  assert(catalog.levels.length > 0, "Catalog must contain at least one level.");
  assert(catalog.definitions.length > 0, "Catalog must contain at least one object definition.");
  uniqueIds(catalog.levels, "level");
  uniqueIds(catalog.definitions, "definition");

  const definitions = new Map();
  for (const definition of catalog.definitions) {
    assert(typeof definition.name === "string" && definition.name.trim(), `${definition.id}: name is required.`);
    assert(KINDS.has(definition.kind), `${definition.id}: unsupported kind '${definition.kind}'.`);
    for (const key of ["radius", "width", "height", "mass", "fieldRadius", "softening", "hitPoints", "damageThreshold"])
      assertFinite(definition[key], `${definition.id}.${key}`, 0, 100000);
    definitions.set(definition.id, definition);
  }

  for (const level of catalog.levels) {
    assert(typeof level.name === "string" && level.name.trim(), `${level.id}: name is required.`);
    assertFinite(level.par, `${level.id}.par`, 1, 999);
    assertFinite(level.timeLimit, `${level.id}.timeLimit`, 1, 86400);
    assert(Array.isArray(level.objects), `${level.id}: objects must be an array.`);
    uniqueIds(level.objects, `${level.id} object`);

    const counts = { cannon: 0, target: 0, gravity: 0 };
    for (const object of level.objects) {
      const definition = definitions.get(object.definitionId);
      assert(definition, `${level.id}.${object.id}: unknown definition '${object.definitionId}'.`);
      const kind = object.kind || definition.kind;
      assert(KINDS.has(kind), `${level.id}.${object.id}: unsupported kind '${kind}'.`);
      for (const key of ["x", "y", "z", "rotation"])
        assertFinite(object[key] ?? 0, `${level.id}.${object.id}.${key}`);
      for (const key of ["scale", "radius", "width", "height", "mass", "fieldRadius", "softening", "hitPoints", "damageThreshold", "orbitRadius", "orbitSpeed", "startAngle"])
        if (object[key] !== undefined) assertFinite(object[key], `${level.id}.${object.id}.${key}`, 0, 100000);
      if (kind === "cannon") counts.cannon++;
      if (kind === "target") counts.target++;
      if (["planet", "sun", "blackHole", "moon"].includes(kind)) counts.gravity++;
    }
    assert(counts.cannon === 1, `${level.id} must contain exactly one cannon.`);
    assert(counts.target > 0, `${level.id} must contain at least one target.`);
    assert(counts.gravity > 0, `${level.id} must contain at least one gravity body.`);
  }
  return catalog;
}

async function readJson(file) {
  return JSON.parse(await fs.readFile(file, "utf8"));
}

async function loadCatalog(paths = DEFAULT_PATHS) {
  const [levels, definitions] = await Promise.all([readJson(paths.levels), readJson(paths.definitions)]);
  return validateCatalog({ levels: levels.levels, definitions: definitions.definitions });
}

async function writeJsonAtomic(file, value) {
  const temp = `${file}.${process.pid}.tmp`;
  await fs.mkdir(path.dirname(file), { recursive: true });
  await fs.writeFile(temp, `${JSON.stringify(value, null, 2)}\n`, "utf8");
  await fs.rename(temp, file);
}

async function saveCatalog(paths, catalog) {
  validateCatalog(catalog);
  await writeJsonAtomic(paths.levels, { levels: catalog.levels });
  await writeJsonAtomic(paths.definitions, { definitions: catalog.definitions });
}

function readBody(request) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;
    request.on("data", chunk => {
      size += chunk.length;
      if (size > MAX_BODY_BYTES) {
        reject(new Error("Request body exceeds 2 MB."));
        request.destroy();
      } else chunks.push(chunk);
    });
    request.on("end", () => {
      try { resolve(JSON.parse(Buffer.concat(chunks).toString("utf8"))); }
      catch { reject(new Error("Request body must be valid JSON.")); }
    });
    request.on("error", reject);
  });
}

function sendJson(response, status, value) {
  const body = JSON.stringify(value);
  response.writeHead(status, { "content-type": "application/json; charset=utf-8", "content-length": Buffer.byteLength(body) });
  response.end(body);
}

function contentType(file) {
  return { ".html": "text/html", ".css": "text/css", ".js": "text/javascript", ".svg": "image/svg+xml" }[path.extname(file)] || "application/octet-stream";
}

function createServer(customPaths = {}) {
  const paths = { ...DEFAULT_PATHS, ...customPaths };
  return http.createServer(async (request, response) => {
    try {
      const url = new URL(request.url, "http://localhost");
      if (url.pathname === "/api/catalog" && request.method === "GET") {
        sendJson(response, 200, await loadCatalog(paths));
        return;
      }
      if (url.pathname === "/api/catalog" && request.method === "PUT") {
        const catalog = await readBody(request);
        await saveCatalog(paths, catalog);
        sendJson(response, 200, { ok: true });
        return;
      }
      if (url.pathname.startsWith("/api/")) {
        sendJson(response, 404, { error: "Not found." });
        return;
      }

      const relative = url.pathname === "/" ? "index.html" : url.pathname.slice(1);
      const file = path.resolve(paths.publicDir, relative);
      assert(file.startsWith(`${path.resolve(paths.publicDir)}${path.sep}`), "Invalid path.");
      const body = await fs.readFile(file);
      response.writeHead(200, { "content-type": `${contentType(file)}; charset=utf-8`, "content-length": body.length });
      response.end(body);
    } catch (error) {
      const status = error.code === "ENOENT" ? 404 : 400;
      sendJson(response, status, { error: error.message });
    }
  });
}

if (require.main === module) {
  const port = Number(process.env.PORT || 4173);
  createServer().listen(port, "127.0.0.1", () => {
    console.log(`Cannon level editor: http://127.0.0.1:${port}`);
  });
}

module.exports = { createServer, loadCatalog, saveCatalog, validateCatalog };
