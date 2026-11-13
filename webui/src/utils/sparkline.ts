// Geometry for the small charts in the Node Health dialog. Values are one per
// time interval, oldest first; null means nothing was measured in that interval.

export type Series = (number | null | undefined)[];

export interface Bar {
  index: number;
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface Line {
  // SVG path data, one entry per unbroken run of two or more values.
  paths: string[];
  // Values with no neighbor to connect to.
  dots: { index: number; x: number; y: number }[];
}

const isValue = (v: number | null | undefined): v is number =>
  v != null && Number.isFinite(v);

const round = (n: number) => Math.round(n * 100) / 100;

// The value range a chart spans. Bars always grow from zero. Lines get a little
// room around their own range, so a steady series doesn't hug an edge.
export function domain(values: Series, bars: boolean): [number, number] {
  const present = values.filter(isValue);
  if (!present.length) return [0, 1];
  const max = Math.max(...present);
  if (bars) return [0, max > 0 ? max : 1];
  const min = Math.min(...present);
  const pad = max > min ? (max - min) * 0.1 : Math.abs(max) * 0.1 || 1;
  return [Math.max(0, min - pad), max + pad];
}

// Horizontal center of an interval's slot.
export function slotX(index: number, count: number, width: number) {
  return ((index + 0.5) * width) / Math.max(count, 1);
}

// The interval under a horizontal pointer position.
export function slotAt(x: number, count: number, width: number) {
  if (count <= 0 || width <= 0) return 0;
  return Math.min(count - 1, Math.max(0, Math.floor((x / width) * count)));
}

export function barRects(values: Series, width: number, height: number): Bar[] {
  const [, max] = domain(values, true);
  const slot = width / Math.max(values.length, 1);
  // Leave a gap between neighbors, and never let a bar get fat.
  const barWidth = Math.min(24, Math.max(1, slot - (slot >= 6 ? 2 : 1)));
  const bars: Bar[] = [];
  values.forEach((v, index) => {
    if (!isValue(v) || v <= 0) return;
    // Anything above zero stays visible.
    const h = Math.max(1, (v / max) * height);
    bars.push({
      index,
      x: round(slotX(index, values.length, width) - barWidth / 2),
      y: round(height - h),
      width: round(barWidth),
      height: round(h),
    });
  });
  return bars;
}

// Vertical position of a value on a line chart. The line keeps `inset` clear at
// the top and bottom, so its stroke and markers aren't clipped.
export function lineY(v: number, values: Series, height: number, inset = 0) {
  const [min, max] = domain(values, false);
  const inner = height - inset * 2;
  return round(inset + inner - ((v - min) / (max - min)) * inner);
}

export function linePaths(
  values: Series,
  width: number,
  height: number,
  inset = 0
): Line {
  const point = (v: number, index: number) => ({
    index,
    x: round(slotX(index, values.length, width)),
    y: lineY(v, values, height, inset),
  });

  const line: Line = { paths: [], dots: [] };
  let run: { index: number; x: number; y: number }[] = [];
  const flush = () => {
    if (run.length === 1) line.dots.push(run[0]);
    else if (run.length > 1)
      line.paths.push(run.map((p, i) => `${i ? "L" : "M"}${p.x},${p.y}`).join(""));
    run = [];
  };
  values.forEach((v, index) => {
    if (isValue(v)) run.push(point(v, index));
    else flush();
  });
  flush();
  return line;
}
