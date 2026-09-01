"use strict";

const stage = document.querySelector("#stage");
const context = stage.getContext("2d");
const levelSelect = document.querySelector("#levelSelect");
const palette = document.querySelector("#palette");
const levelInspector = document.querySelector("#levelInspector");
const objectInspector = document.querySelector("#objectInspector");
const status = document.querySelector("#status");
const snapSelect = document.querySelector("#snapSelect");
const definitionDialog = document.querySelector("#definitionDialog");
const definitionForm = document.querySelector("#definitionForm");
const { round, snapCoordinate, snapRectangle } = window.CannonEditorModel;

const PIXELS_PER_UNIT = 16;
const gravityKinds = new Set(["planet", "sun", "blackHole", "moon"]);
let catalog = { levels: [], definitions: [] };
let savedCatalog = { levels: [], definitions: [] };
let levelIndex = 0;
let selectedId = null;
let pointerDrag = null;
let dirty = false;
let assetSourceId = null;
let viewWidth = stage.width;
let viewHeight = stage.height;

function currentLevel() { return catalog.levels[levelIndex]; }
function definition(id) { return catalog.definitions.find(item => item.id === id); }
function selectedObject() { return currentLevel()?.objects.find(item => item.id === selectedId); }
function kindOf(object) { return object.kind || definition(object.definitionId)?.kind; }
function valueOf(object, key) { return object[key] ?? definition(object.definitionId)?.[key] ?? 0; }
function worldToScreen(x, y) { return { x: viewWidth / 2 + x * PIXELS_PER_UNIT, y: viewHeight / 2 - y * PIXELS_PER_UNIT }; }
function screenToWorld(x, y) { return { x: (x - viewWidth / 2) / PIXELS_PER_UNIT, y: (viewHeight / 2 - y) / PIXELS_PER_UNIT }; }
function snappedPosition(object, x, y) {
  const step = Number(snapSelect.value);
  const rectangular = ["block", "explosiveBlock", "cannon"].includes(kindOf(object));
  const scale = valueOf(object, "scale") || 1;
  return rectangular
    ? snapRectangle(x, y, valueOf(object, "width"), valueOf(object, "height"), scale, step)
    : { x: snapCoordinate(x, step), y: snapCoordinate(y, step) };
}

function syncStageSize() {
  const bounds = stage.getBoundingClientRect();
  viewWidth = Math.max(1, Math.round(bounds.width));
  viewHeight = Math.max(1, Math.round(bounds.height));
  const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);
  const bufferWidth = Math.round(viewWidth * pixelRatio);
  const bufferHeight = Math.round(viewHeight * pixelRatio);
  if (stage.width !== bufferWidth || stage.height !== bufferHeight) {
    stage.width = bufferWidth;
    stage.height = bufferHeight;
  }
  context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
}

function setStatus(message, isError = false) {
  status.textContent = message;
  status.style.color = isError ? "#ff9ca5" : "#aeb6d5";
}

function markDirty() {
  dirty = true;
  setStatus("Unsaved changes");
}

function fillSelect() {
  levelSelect.replaceChildren();
  catalog.levels.forEach((level, index) => {
    const option = document.createElement("option");
    option.value = index;
    option.textContent = level.name;
    levelSelect.append(option);
  });
  levelSelect.value = levelIndex;
}

function renderPalette() {
  palette.replaceChildren();
  for (const item of catalog.definitions) {
    const button = document.createElement("button");
    button.className = "palette-item";
    button.draggable = true;
    button.dataset.definitionId = item.id;
    const swatch = document.createElement("span");
    swatch.className = `swatch ${item.kind.includes("Block") || item.kind === "block" || item.kind === "cannon" ? "block" : ""}`;
    swatch.style.background = item.color;
    const label = document.createElement("span");
    label.textContent = `${item.name} · ${item.kind}`;
    button.append(swatch, label);
    button.addEventListener("dragstart", event => event.dataTransfer.setData("text/cannon-definition", item.id));
    palette.append(button);
  }
}

function drawGrid() {
  context.clearRect(0, 0, viewWidth, viewHeight);
  context.fillStyle = "#0e1220";
  context.fillRect(0, 0, viewWidth, viewHeight);
  context.lineWidth = 1;
  for (let x = viewWidth / 2 % PIXELS_PER_UNIT; x < viewWidth; x += PIXELS_PER_UNIT) {
    context.strokeStyle = Math.abs(x - viewWidth / 2) < 1 ? "#536080" : "#202840";
    context.beginPath(); context.moveTo(x, 0); context.lineTo(x, viewHeight); context.stroke();
  }
  for (let y = viewHeight / 2 % PIXELS_PER_UNIT; y < viewHeight; y += PIXELS_PER_UNIT) {
    context.strokeStyle = Math.abs(y - viewHeight / 2) < 1 ? "#536080" : "#202840";
    context.beginPath(); context.moveTo(0, y); context.lineTo(viewWidth, y); context.stroke();
  }
}

function drawObject(object) {
  const kind = kindOf(object);
  const point = worldToScreen(object.x || 0, object.y || 0);
  const scale = valueOf(object, "scale") || 1;
  const radius = valueOf(object, "radius") * scale * PIXELS_PER_UNIT;
  const color = object.color || definition(object.definitionId)?.color || "#aaa";

  if (gravityKinds.has(kind)) {
    const field = valueOf(object, "fieldRadius") * PIXELS_PER_UNIT;
    context.save();
    context.strokeStyle = "#8296ff66"; context.setLineDash([7, 7]); context.lineWidth = 1;
    context.beginPath(); context.arc(point.x, point.y, field, 0, Math.PI * 2); context.stroke();
    context.restore();
  }

  context.save();
  context.translate(point.x, point.y);
  context.rotate(-(object.rotation || 0) * Math.PI / 180);
  context.fillStyle = color;
  context.strokeStyle = object.id === selectedId ? "#ffffff" : "#ffffff66";
  context.lineWidth = object.id === selectedId ? 3 : 1;

  if (kind === "block" || kind === "explosiveBlock" || kind === "cannon") {
    const width = valueOf(object, "width") * scale * PIXELS_PER_UNIT;
    const height = valueOf(object, "height") * scale * PIXELS_PER_UNIT;
    context.fillRect(-width / 2, -height / 2, width, height);
    context.strokeRect(-width / 2, -height / 2, width, height);
  } else {
    context.beginPath(); context.arc(0, 0, Math.max(radius, 5), 0, Math.PI * 2); context.fill(); context.stroke();
    if (kind === "target") {
      context.fillStyle = "#101522";
      context.beginPath(); context.arc(-radius * .28, -radius * .15, Math.max(2, radius * .12), 0, Math.PI * 2); context.fill();
      context.beginPath(); context.arc(radius * .28, -radius * .15, Math.max(2, radius * .12), 0, Math.PI * 2); context.fill();
    }
  }
  context.restore();

  context.fillStyle = "#dfe4ff";
  context.font = "12px system-ui";
  context.fillText(object.id, point.x + 8, point.y - 8);
}

function renderStage() {
  syncStageSize();
  drawGrid();
  for (const object of currentLevel()?.objects || []) drawObject(object);
}

function makeField(label, value, options = {}) {
  const wrapper = document.createElement("label");
  wrapper.className = "field";
  wrapper.textContent = label;
  const input = document.createElement(options.options ? "select" : "input");
  if (options.options) {
    for (const optionValue of options.options) {
      const option = document.createElement("option");
      option.value = optionValue.value;
      option.textContent = optionValue.label;
      input.append(option);
    }
  } else {
    input.type = options.type || "number";
    if (input.type === "number") { input.step = options.step || "0.1"; input.min = options.min ?? ""; }
  }
  input.value = value;
  if (options.readOnly) input.readOnly = true;
  wrapper.append(input);
  return { wrapper, input };
}

function sectionLabel(text) {
  const element = document.createElement("div");
  element.className = "section-label";
  element.textContent = text;
  return element;
}

function bindField(parent, label, object, key, options = {}) {
  const field = makeField(label, options.displayValue ?? object[key] ?? "", options);
  field.input.addEventListener("input", () => {
    object[key] = options.type === "text" || options.options ? field.input.value : Number(field.input.value);
    markDirty();
    if (options.render !== false) renderStage();
    if (options.onChange) options.onChange();
  });
  parent.append(field.wrapper);
}

function renderLevelInspector() {
  levelInspector.replaceChildren();
  const level = currentLevel();
  if (!level) return;
  levelInspector.append(sectionLabel("Level record"));
  bindField(levelInspector, "ID", level, "id", { type: "text", readOnly: true, render: false });
  bindField(levelInspector, "Name", level, "name", { type: "text", render: false, onChange: fillSelect });
  bindField(levelInspector, "Par shots", level, "par", { min: 1, step: 1, render: false });
  bindField(levelInspector, "Time limit (seconds)", level, "timeLimit", { min: 1, step: 1, render: false });
}

function renderObjectInspector() {
  objectInspector.replaceChildren();
  const object = selectedObject();
  if (!object) {
    objectInspector.className = "empty";
    objectInspector.textContent = "Select object.";
    return;
  }
  objectInspector.className = "";
  objectInspector.append(sectionLabel("Object instance"));
  bindField(objectInspector, "ID", object, "id", { type: "text", readOnly: true, render: false });
  bindField(objectInspector, "Definition", object, "definitionId", {
    options: catalog.definitions.map(item => ({ value: item.id, label: `${item.name} · ${item.kind}` })),
    onChange: () => { renderPalette(); renderObjectInspector(); }
  });

  objectInspector.append(sectionLabel("Transform"));
  for (const [label, key] of [["X", "x"], ["Y", "y"], ["Z", "z"], ["Rotation °", "rotation"]])
    bindField(objectInspector, label, object, key);
  bindField(objectInspector, "Scale", object, "scale", { min: 0.1 });
  const sizeRow = document.createElement("div");
  sizeRow.className = "size-row";
  for (const [text, factor] of [["− smaller", 0.9], ["+ larger", 1.1]]) {
    const button = document.createElement("button");
    button.textContent = text;
    button.addEventListener("click", () => {
      object.scale = round(Math.max(0.1, valueOf(object, "scale") * factor));
      markDirty(); renderObjectInspector(); renderStage();
    });
    sizeRow.append(button);
  }
  objectInspector.append(sizeRow);

  const snapButton = document.createElement("button");
  snapButton.textContent = `Snap position to ${snapSelect.value === "0" ? "grid off" : snapSelect.value}`;
  snapButton.disabled = snapSelect.value === "0";
  snapButton.addEventListener("click", () => {
    const position = snappedPosition(object, object.x || 0, object.y || 0);
    object.x = position.x;
    object.y = position.y;
    markDirty(); renderObjectInspector(); renderStage();
  });
  objectInspector.append(snapButton);

  const createAsset = document.createElement("button");
  createAsset.id = "createAssetFromObject";
  createAsset.textContent = "Create asset from instance…";
  createAsset.addEventListener("click", () => openAssetDialog(object));
  objectInspector.append(createAsset);

  objectInspector.append(sectionLabel("Exact metrics"));
  const metrics = [
    ["Radius", "radius"], ["Width", "width"], ["Height", "height"],
    ["Gravity strength / mass", "mass"], ["Gravity field radius", "fieldRadius"],
    ["Gravity softening", "softening"], ["Hit points", "hitPoints"],
    ["Damage threshold", "damageThreshold"], ["Surface center X", "surfaceCenterX"],
    ["Surface center Y", "surfaceCenterY"], ["Orbit radius", "orbitRadius"],
    ["Orbit speed °/s", "orbitSpeed"], ["Orbit start angle °", "startAngle"]
  ];
  for (const [label, key] of metrics)
    bindField(objectInspector, label, object, key, { displayValue: valueOf(object, key), min: ["surfaceCenterX", "surfaceCenterY"].includes(key) ? undefined : 0 });

  const color = makeField("Color override", object.color || definition(object.definitionId)?.color || "#ffffff", { type: "color" });
  color.input.addEventListener("input", () => { object.color = color.input.value; markDirty(); renderStage(); });
  objectInspector.append(color.wrapper);

  const remove = document.createElement("button");
  remove.className = "danger";
  remove.textContent = "Delete object";
  remove.addEventListener("click", () => {
    currentLevel().objects = currentLevel().objects.filter(item => item.id !== object.id);
    selectedId = null; markDirty(); renderObjectInspector(); renderStage();
  });
  objectInspector.append(remove);
}

function uniqueObjectId(base) {
  let index = 1;
  let candidate = `${base}-${index}`;
  const ids = new Set(currentLevel().objects.map(item => item.id));
  while (ids.has(candidate)) candidate = `${base}-${++index}`;
  return candidate;
}

function addObject(definitionId, x, y) {
  const source = definition(definitionId);
  if (!source) return;
  const object = {
    id: uniqueObjectId(source.id), definitionId: source.id,
    x: 0, y: 0, z: 0, rotation: 0, scale: 1,
    radius: source.radius, width: source.width, height: source.height,
    mass: source.mass, fieldRadius: source.fieldRadius, softening: source.softening,
    hitPoints: source.hitPoints, damageThreshold: source.damageThreshold,
    surfaceCenterX: 0, surfaceCenterY: 0, orbitRadius: 0, orbitSpeed: 0, startAngle: 0
  };
  const position = snappedPosition(object, x, y);
  object.x = position.x;
  object.y = position.y;
  currentLevel().objects.push(object);
  selectedId = object.id;
  markDirty(); renderObjectInspector(); renderStage();
}

function canvasPoint(event) {
  const rect = stage.getBoundingClientRect();
  return { x: event.clientX - rect.left, y: event.clientY - rect.top };
}

function hitTest(point) {
  const objects = [...currentLevel().objects].reverse();
  return objects.find(object => {
    const center = worldToScreen(object.x || 0, object.y || 0);
    const scale = valueOf(object, "scale") || 1;
    const kind = kindOf(object);
    if (kind === "block" || kind === "explosiveBlock" || kind === "cannon") {
      const halfWidth = valueOf(object, "width") * scale * PIXELS_PER_UNIT / 2;
      const halfHeight = valueOf(object, "height") * scale * PIXELS_PER_UNIT / 2;
      return Math.abs(point.x - center.x) <= halfWidth && Math.abs(point.y - center.y) <= halfHeight;
    }
    const radius = Math.max(8, valueOf(object, "radius") * scale * PIXELS_PER_UNIT);
    return Math.hypot(point.x - center.x, point.y - center.y) <= radius;
  });
}

stage.addEventListener("dragover", event => event.preventDefault());
stage.addEventListener("drop", event => {
  event.preventDefault();
  const point = canvasPoint(event);
  const world = screenToWorld(point.x, point.y);
  addObject(event.dataTransfer.getData("text/cannon-definition"), world.x, world.y);
});
stage.addEventListener("pointerdown", event => {
  const point = canvasPoint(event);
  const object = hitTest(point);
  selectedId = object?.id || null;
  pointerDrag = object ? { id: object.id } : null;
  stage.setPointerCapture(event.pointerId);
  renderObjectInspector(); renderStage();
});
stage.addEventListener("pointermove", event => {
  if (!pointerDrag) return;
  const object = selectedObject();
  const point = canvasPoint(event);
  const world = screenToWorld(point.x, point.y);
  const position = snappedPosition(object, world.x, world.y);
  object.x = position.x; object.y = position.y;
  markDirty(); renderObjectInspector(); renderStage();
});
stage.addEventListener("pointerup", () => { pointerDrag = null; });

levelSelect.addEventListener("change", () => {
  levelIndex = Number(levelSelect.value); selectedId = null;
  renderLevelInspector(); renderObjectInspector(); renderStage();
});

snapSelect.addEventListener("change", () => renderObjectInspector());

document.querySelector("#newLevel").addEventListener("click", () => {
  const name = prompt("Level name", `Level ${catalog.levels.length + 1}`);
  if (!name) return;
  let id = name.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || `level-${catalog.levels.length + 1}`;
  const ids = new Set(catalog.levels.map(level => level.id));
  let suffix = 2, base = id;
  while (ids.has(id)) id = `${base}-${suffix++}`;
  catalog.levels.push({ id, name, par: 3, timeLimit: 180, objects: [] });
  levelIndex = catalog.levels.length - 1; selectedId = null; markDirty(); renderAll();
});

document.querySelector("#duplicateLevel").addEventListener("click", () => {
  const copy = structuredClone(currentLevel());
  copy.id = `${copy.id}-copy`;
  let suffix = 2;
  while (catalog.levels.some(level => level.id === copy.id)) copy.id = `${currentLevel().id}-copy-${suffix++}`;
  copy.name = `${copy.name} Copy`;
  copy.objects.forEach(object => object.id = `${object.id}-copy`);
  catalog.levels.push(copy); levelIndex = catalog.levels.length - 1; selectedId = null; markDirty(); renderAll();
});

document.querySelector("#deleteLevel").addEventListener("click", () => {
  if (catalog.levels.length <= 1) return setStatus("Catalog needs at least one level.", true);
  if (!confirm(`Delete ${currentLevel().name}?`)) return;
  catalog.levels.splice(levelIndex, 1); levelIndex = Math.max(0, levelIndex - 1); selectedId = null; markDirty(); renderAll();
});

document.querySelector("#resetCatalog").addEventListener("click", () => {
  if (!dirty) return setStatus("Nothing to reset.");
  if (!confirm("Discard all unsaved level and asset changes?")) return;
  const levelId = currentLevel()?.id;
  catalog = structuredClone(savedCatalog);
  levelIndex = Math.max(0, catalog.levels.findIndex(level => level.id === levelId));
  selectedId = null;
  dirty = false;
  renderAll();
  setStatus("Unsaved changes reset.");
});

document.querySelector("#saveCatalog").addEventListener("click", async () => {
  try {
    setStatus("Saving…");
    const response = await fetch("/api/catalog", { method: "PUT", headers: { "content-type": "application/json" }, body: JSON.stringify(catalog) });
    const result = await response.json();
    if (!response.ok) throw new Error(result.error || "Save failed.");
    savedCatalog = structuredClone(catalog);
    dirty = false; setStatus(`Saved ${catalog.levels.length} level records and ${catalog.definitions.length} definitions.`);
  } catch (error) { setStatus(error.message, true); }
});

function uniqueDefinitionId(base) {
  let candidate = base.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || "custom-asset";
  const ids = new Set(catalog.definitions.map(item => item.id));
  const root = candidate;
  let suffix = 2;
  while (ids.has(candidate)) candidate = `${root}-${suffix++}`;
  return candidate;
}

function openAssetDialog(source = null) {
  definitionForm.reset();
  assetSourceId = source?.id || null;
  if (source) {
    const sourceDefinition = definition(source.definitionId);
    definitionForm.elements.id.value = uniqueDefinitionId(`${source.id}-asset`);
    definitionForm.elements.name.value = `${sourceDefinition?.name || source.id} Custom`;
    definitionForm.elements.kind.value = kindOf(source);
    definitionForm.elements.color.value = source.color || sourceDefinition?.color || "#9eaeb8";
    for (const key of ["radius", "width", "height", "mass", "fieldRadius", "softening", "hitPoints", "damageThreshold"])
      definitionForm.elements[key].value = valueOf(source, key);
  }
  definitionDialog.showModal();
}

document.querySelector("#newDefinition").addEventListener("click", () => openAssetDialog());
definitionForm.addEventListener("submit", event => {
  event.preventDefault();
  if (event.submitter?.value === "cancel") {
    assetSourceId = null;
    definitionDialog.close();
    return;
  }
  const values = Object.fromEntries(new FormData(definitionForm));
  if (catalog.definitions.some(item => item.id === values.id)) return setStatus(`Definition '${values.id}' already exists.`, true);
  for (const key of ["radius", "width", "height", "mass", "fieldRadius", "softening", "hitPoints", "damageThreshold"])
    values[key] = Number(values[key]);
  catalog.definitions.push(values);
  const source = currentLevel()?.objects.find(item => item.id === assetSourceId);
  if (source) source.definitionId = values.id;
  assetSourceId = null;
  definitionDialog.close(); definitionForm.reset(); markDirty(); renderPalette();
  renderObjectInspector(); renderStage();
});

function renderAll() {
  fillSelect(); renderPalette(); renderLevelInspector(); renderObjectInspector(); renderStage();
}

async function load() {
  try {
    const response = await fetch("/api/catalog");
    const result = await response.json();
    if (!response.ok) throw new Error(result.error || "Load failed.");
    catalog = result;
    savedCatalog = structuredClone(catalog);
    renderAll();
    setStatus(`Loaded ${catalog.levels.length} levels and ${catalog.definitions.length} definitions.`);
  } catch (error) { setStatus(error.message, true); }
}

window.addEventListener("beforeunload", event => { if (dirty) event.preventDefault(); });
new ResizeObserver(() => renderStage()).observe(stage);
load();
