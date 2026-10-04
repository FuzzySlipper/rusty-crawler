/**
 * The automap: what the party has walked of the place it stands in, drawn from the projection and nothing else.
 *
 * The shapes are SVG in the drawing's own coordinate space — the product says where each run of squares is, where
 * every mark stands, how large a mark is drawn, and the corners of the party's own marker — so this companion
 * places what it was given and works out no scale, no offset, no size, and no position of its own. The product
 * also names the directions at the drawing's edges, so a panel can label its source row order without guessing.
 * Nothing is remembered between snapshots, so the same projection draws the same map however many times it arrives.
 */

import type { Fields } from './reader.js';
import { button, element, section, SVG_NAMESPACE, type Host, type Section } from './dom.js';

/** One rectangle of the drawing: a run of squares the automap fills with one colour. */
export interface MapCellView {
  readonly x: number;
  readonly y: number;
  readonly w: number;
  readonly h: number;
  readonly kind: string;
}

/** One thing the automap marks: a place's own feature, or what a detection revealed. */
export interface MapMarkView {
  readonly id: string;
  readonly kind: string;
  readonly label: string;
  readonly x: number;
  readonly y: number;
  readonly detected: boolean;
}

/** The directions the product assigns to the drawing's four screen edges. */
export interface MapOrientationView {
  readonly top: string;
  readonly bottom: string;
  readonly left: string;
  readonly right: string;
}

/** The drawing itself, in the drawing's own space: every number here is ready to place. */
export interface MapDrawingView {
  readonly rung: number;
  readonly rungs: number;
  readonly cells: number;
  readonly size: number;
  readonly cellsDrawn: readonly MapCellView[];
  readonly marks: readonly MapMarkView[];
  readonly partyX: number;
  readonly partyY: number;
  readonly facing: number;
  /** How large a mark is drawn, never zero. */
  readonly markRadius: number;
  /** The party marker's corners, as x and y in turn, before the facing turns it. */
  readonly partyPoints: readonly number[];
}

/**
 * The automap. A session whose ruleset stated no automap, a place content carries no map for, and a place nothing
 * has been walked of are three different facts, and `available`, `mapped`, and the state sentence tell them apart.
 */
export interface MapView {
  readonly available: boolean;
  readonly mapped: boolean;
  readonly title: string;
  readonly place: string;
  readonly name: string;
  readonly kind: string;
  readonly state: string;
  readonly seen: number;
  readonly total: number;
  readonly detection: string;
  readonly detectionMessage: string;
  readonly detectionEnds: string;
  readonly orientation: MapOrientationView;
  readonly drawing: MapDrawingView | null;
}

export function readMap(f: Fields): MapView {
  return {
    available: f.flag('available'),
    mapped: f.flag('mapped'),
    title: f.text('title'),
    place: f.text('place'),
    name: f.text('name'),
    kind: f.text('kind'),
    state: f.text('state'),
    seen: f.number('seen'),
    total: f.number('total'),
    detection: f.text('detection'),
    detectionMessage: f.text('detectionMessage'),
    detectionEnds: f.text('detectionEnds'),
    orientation: (() => {
      const orientation = f.object('orientation');
      return {
        top: orientation.text('top'),
        bottom: orientation.text('bottom'),
        left: orientation.text('left'),
        right: orientation.text('right'),
      };
    })(),
    drawing: f.nullable('drawing', (drawing) => ({
      rung: drawing.number('rung'),
      rungs: drawing.number('rungs'),
      cells: drawing.number('cells'),
      size: drawing.number('size'),
      cellsDrawn: drawing.list('cellsDrawn', (run) => ({
        x: run.number('x'),
        y: run.number('y'),
        w: run.number('w'),
        h: run.number('h'),
        kind: run.text('kind'),
      })),
      marks: drawing.list('marks', (mark) => ({
        id: mark.text('id'),
        kind: mark.text('kind'),
        label: mark.text('label'),
        x: mark.number('x'),
        y: mark.number('y'),
        detected: mark.flag('detected'),
      })),
      partyX: drawing.number('partyX'),
      partyY: drawing.number('partyY'),
      facing: drawing.number('facing'),
      markRadius: drawing.number('markRadius'),
      partyPoints: drawing.numbers('partyPoints'),
    })),
  };
}

/** The zoom steps the automap book offers, as how many times the drawing is drawn larger. */
const ZOOMS = ['1', '2', '4'] as const;

/**
 * Mounts the automap.
 * @param zoomable Whether the drawing offers zoom, which the map book does and the adventure frame's small map does not.
 */
export function mountMap(host: Host, zoomable = false): Section<MapView> {
  const { panel } = host;
  const map = section('crawler-map');
  const mapHead = element('p', 'crawler-step-head');
  const state = element('p', 'crawler-map-state');
  const orientation = element('p', 'crawler-map-orientation');
  orientation.hidden = true;
  const detection = element('p', 'crawler-map-detection');
  detection.hidden = true;
  const drawing = document.createElementNS(SVG_NAMESPACE, 'svg');
  drawing.setAttribute('class', 'crawler-map-drawing');
  drawing.setAttribute('preserveAspectRatio', 'xMidYMid meet');
  const cells = document.createElementNS(SVG_NAMESPACE, 'g');
  cells.setAttribute('class', 'crawler-map-cells');
  const marks = document.createElementNS(SVG_NAMESPACE, 'g');
  marks.setAttribute('class', 'crawler-map-marks');
  const party = document.createElementNS(SVG_NAMESPACE, 'polygon');
  party.setAttribute('class', 'crawler-map-party');
  drawing.append(cells, marks, party);
  // The drawing sits in a frame that scrolls when it is drawn larger; zooming scrolls the party's marker into view.
  const frame = element('div', 'crawler-map-frame');
  frame.append(drawing);
  let zoom: (typeof ZOOMS)[number] = '1';
  frame.dataset.zoom = zoom;
  const controls = element('div', 'crawler-map-zoom');
  const centre = (): void => {
    if (typeof party.scrollIntoView === 'function') party.scrollIntoView({ block: 'center', inline: 'center' });
  };
  const step = (by: -1 | 1): void => {
    const at = ZOOMS.indexOf(zoom);
    const next = ZOOMS[Math.min(ZOOMS.length - 1, Math.max(0, at + by))];
    if (next === undefined) return;
    zoom = next;
    frame.dataset.zoom = zoom;
    zoomLabel.textContent = `×${zoom}`;
    centre();
  };
  const zoomOut = button('−', 'crawler-map-zoom-out');
  zoomOut.title = 'Zoom out';
  zoomOut.addEventListener('click', () => step(-1));
  const zoomLabel = element('span', 'crawler-map-zoom-level');
  zoomLabel.textContent = '×1';
  const zoomIn = button('+', 'crawler-map-zoom-in');
  zoomIn.title = 'Zoom in';
  zoomIn.addEventListener('click', () => step(1));
  const find = button('Find the party', 'crawler-map-find');
  find.addEventListener('click', centre);
  controls.append(zoomOut, zoomLabel, zoomIn, find);
  controls.hidden = !zoomable;
  map.append(mapHead, orientation, state, detection, controls, frame);

  const render = (view: MapView): void => {
    panel.dataset.map = !view.available ? 'none' : view.mapped ? 'present' : 'unmapped';
    panel.dataset.mapDetection = view.detection;
    map.hidden = !view.available;
    map.dataset.seen = String(view.seen);
    map.dataset.total = String(view.total);
    mapHead.textContent = view.title === '' ? 'Automap' : `${view.title}${view.name === '' ? '' : ` · ${view.name}`}`;
    const hasOrientation = Object.values(view.orientation).some((word) => word !== '');
    orientation.hidden = view.drawing === null || !hasOrientation;
    orientation.textContent = hasOrientation
      ? `Top: ${view.orientation.top} · Right: ${view.orientation.right} · Bottom: ${view.orientation.bottom} · Left: ${view.orientation.left}`
      : '';
    state.textContent = view.state;
    detection.hidden = view.detectionMessage === '';
    detection.textContent = view.detectionEnds === '' ? view.detectionMessage : `${view.detectionMessage} Until ${view.detectionEnds}.`;
    const shape = view.drawing;
    // An SVG element has no `hidden` property in the DOM's own types, so the attribute is what says whether there is
    // a drawing at all; the stylesheet is what hides it.
    if (shape === null) {
      drawing.setAttribute('hidden', '');
      drawing.removeAttribute('viewBox');
      cells.replaceChildren();
      marks.replaceChildren();
      party.removeAttribute('points');
      party.removeAttribute('transform');
      return;
    }

    drawing.removeAttribute('hidden');
    drawing.setAttribute('viewBox', `0 0 ${shape.size} ${shape.size}`);
    drawing.dataset.rung = `${shape.rung}/${shape.rungs}`;
    cells.replaceChildren(
      ...shape.cellsDrawn.map((run) => {
        const cell = document.createElementNS(SVG_NAMESPACE, 'rect');
        cell.setAttribute('x', String(run.x));
        cell.setAttribute('y', String(run.y));
        cell.setAttribute('width', String(run.w));
        cell.setAttribute('height', String(run.h));
        cell.setAttribute('class', `crawler-map-cell crawler-map-cell-${run.kind}`);
        cell.dataset.kind = run.kind;
        return cell;
      }),
    );
    marks.replaceChildren(
      ...shape.marks.map((mark) => {
        const point = document.createElementNS(SVG_NAMESPACE, 'circle');
        point.setAttribute('cx', String(mark.x));
        point.setAttribute('cy', String(mark.y));
        point.setAttribute('r', String(shape.markRadius));
        point.setAttribute('class', `crawler-map-mark crawler-map-mark-${mark.kind}`);
        point.dataset.id = mark.id;
        point.dataset.kind = mark.kind;
        point.dataset.detected = String(mark.detected);
        const title = document.createElementNS(SVG_NAMESPACE, 'title');
        title.textContent = mark.detected ? `${mark.label} (revealed)` : mark.label;
        point.append(title);
        return point;
      }),
    );
    // The party's own marker: the corners the projection published, turned by the facing it published.
    const corners: string[] = [];
    for (let at = 0; at + 1 < shape.partyPoints.length; at += 2) corners.push(`${shape.partyPoints[at]},${shape.partyPoints[at + 1]}`);
    party.setAttribute('points', corners.join(' '));
    party.setAttribute('transform', `rotate(${shape.facing} ${shape.partyX} ${shape.partyY})`);
  };

  return { element: map, render };
}
