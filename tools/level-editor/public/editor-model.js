"use strict";

(function expose(root) {
  function round(value) {
    return Math.round(value * 100) / 100;
  }

  function snapCoordinate(value, step, edgeOffset = 0) {
    return step > 0
      ? round(Math.round((value - edgeOffset) / step) * step + edgeOffset)
      : round(value);
  }

  function snapRectangle(x, y, width, height, scale, step) {
    return {
      x: snapCoordinate(x, step, width * scale / 2),
      y: snapCoordinate(y, step, height * scale / 2)
    };
  }

  const model = { round, snapCoordinate, snapRectangle };
  root.CannonEditorModel = model;
  if (typeof module !== "undefined") module.exports = model;
})(globalThis);
