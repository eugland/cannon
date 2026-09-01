"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const { createServer, loadCatalog, validateCatalog } = require("./server");

function validCatalog() {
  return {
    definitions: [
      { id: "planet", name: "Planet", kind: "planet", color: "#fff", radius: 4, width: 1, height: 1, mass: 34, fieldRadius: 40, softening: 0.5, hitPoints: 1, damageThreshold: 1 },
      { id: "cannon", name: "Cannon", kind: "cannon", color: "#fff", radius: 1, width: 1, height: 1, mass: 1, fieldRadius: 1, softening: 0.4, hitPoints: 1, damageThreshold: 1 },
      { id: "target", name: "Target", kind: "target", color: "#fff", radius: 1, width: 1, height: 1, mass: 1, fieldRadius: 1, softening: 0.4, hitPoints: 1, damageThreshold: 1 }
    ],
    levels: [{
      id: "level-1", name: "Level 1", par: 3, timeLimit: 180,
      objects: [
        { id: "p", definitionId: "planet", x: 0, y: 0, z: 0, rotation: 0, scale: 1 },
        { id: "c", definitionId: "cannon", x: -10, y: 5, z: 0, rotation: 0, scale: 1 },
        { id: "t", definitionId: "target", x: 2, y: 3, z: 0, rotation: 0, scale: 1 }
      ]
    }]
  };
}

test("validation accepts playable catalog", () => {
  assert.equal(validateCatalog(validCatalog()).levels.length, 1);
});

test("validation rejects missing cannon", () => {
  const catalog = validCatalog();
  catalog.levels[0].objects = catalog.levels[0].objects.filter(object => object.definitionId !== "cannon");
  assert.throws(() => validateCatalog(catalog), /exactly one cannon/);
});

test("shipped Unity records form a valid playable catalog", async () => {
  const catalog = await loadCatalog();

  assert.equal(catalog.levels.length, 10);
  assert.ok(catalog.definitions.length >= 12);
  assert.ok(catalog.levels.every(level => level.objects.length >= 3));
  for (const id of ["stone-brick", "stone-beam", "stone-column", "stone-tower"]) {
    const asset = catalog.definitions.find(definition => definition.id === id);
    assert.ok(asset, `missing castle asset ${id}`);
    assert.equal(Number.isInteger(asset.width * 2), true, `${id} width must align to 0.5 grid`);
    assert.equal(Number.isInteger(asset.height * 2), true, `${id} height must align to 0.5 grid`);
  }
});

test("API saves and reloads catalog records", async context => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "cannon-editor-"));
  const paths = {
    levels: path.join(root, "levels.json"),
    definitions: path.join(root, "objects.json"),
    publicDir: path.join(__dirname, "public")
  };
  await fs.writeFile(paths.levels, JSON.stringify({ levels: validCatalog().levels }));
  await fs.writeFile(paths.definitions, JSON.stringify({ definitions: validCatalog().definitions }));

  const server = createServer(paths);
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  context.after(() => server.close());
  const base = `http://127.0.0.1:${server.address().port}`;

  const catalog = await (await fetch(`${base}/api/catalog`)).json();
  catalog.levels[0].name = "Saved Name";
  const save = await fetch(`${base}/api/catalog`, {
    method: "PUT",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(catalog)
  });

  assert.equal(save.status, 200);
  assert.equal((await (await fetch(`${base}/api/catalog`)).json()).levels[0].name, "Saved Name");
});
