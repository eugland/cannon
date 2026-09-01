"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const { snapCoordinate, snapRectangle } = require("./public/editor-model");

test("point objects snap their centers to selected grid", () => {
  assert.equal(snapCoordinate(1.24, 0.5), 1);
  assert.equal(snapCoordinate(1.26, 0.5), 1.5);
});

test("rectangular assets snap edges despite different dimensions", () => {
  const column = snapRectangle(1.1, 2.2, 0.5, 4, 1, 0.5);
  const brick = snapRectangle(1.1, 2.2, 2, 1, 1, 0.5);

  assert.equal((column.x - 0.25) % 0.5, 0);
  assert.equal((column.y - 2) % 0.5, 0);
  assert.equal((brick.x - 1) % 0.5, 0);
  assert.equal((brick.y - 0.5) % 0.5, 0);
});

test("snap off preserves hundredth-unit precision", () => {
  assert.equal(snapCoordinate(1.237, 0), 1.24);
});
