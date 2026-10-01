/**
 * The fight: who is in it, who may act, what the last order did, and which pacing it is played in.
 *
 * It is shown whether or not anything is hostile — a session with no fight mechanism, a quiet place, and a fight in
 * progress are three different facts. Every number is the product's: the panel runs no countdown of its own,
 * because a screen that ticked a recovery down for itself would show a character ready before the fight agreed, and
 * each control is offered exactly when the product says it would take it.
 */

import type { ControlView } from './overview.js';
import type { Fields } from './reader.js';
import { button, element, head, section, type Host, type Section } from './dom.js';

/** One actor of a fight, as the fight published it. */
export interface FighterView {
  readonly id: string;
  readonly name: string;
  /** Whether the actor may act now, as the fight's own recovery and conditions say. */
  readonly ready: boolean;
  /** How much game time it must still recover, zero when it is ready. */
  readonly recoverySeconds: number;
  /** How far it stands from the party, in the place's own units. */
  readonly distance: number;
  readonly hitPoints: number;
  /** What it can take altogether, zero when nothing here states it. */
  readonly hitPointsMax: number;
  /** What is acting on the actor, in the product's own words, empty when nothing is. */
  readonly conditions: string;
  /** Whether the actor is out of the fight: laid out by what is on it, or taken down by harm. */
  readonly down: boolean;
  /** What a driven actor is doing — `closing`, `backing away`, `holding`, `attacking`, `down` — empty for members. */
  readonly activity: string;
}

/** One actor's place in the order a paced round acts in: the fight's own reading of the recovery that paces real time. */
export interface TurnOrderView {
  readonly id: string;
  readonly name: string;
  /** `party`, `opposition`, `neutral`, or `ally`, as the fight reads the actor's side. */
  readonly side: string;
  readonly remainingSeconds: number;
  readonly ready: boolean;
  /** Whether the fight leaves it able to act at all. */
  readonly canAct: boolean;
  /** Whether it has deferred its turn to the end of the round. */
  readonly waiting: boolean;
  /** Whether this is the actor whose turn it is. */
  readonly current: boolean;
}

/**
 * The round a paced fight is in: which phase, whose turn, and what is left to act. The phase is `none` — or empty —
 * while the fight is played in real time, which is a different fact from a round nobody's turn is in.
 */
export interface TurnView {
  /** `none`, `action`, or `movement`. */
  readonly phase: string;
  readonly round: number;
  readonly actor: string;
  readonly actorName: string;
  /** Whether the product is waiting for the player's committed turn, during which nothing steps. */
  readonly playerTurn: boolean;
  readonly dueSeconds: number;
  readonly roundSeconds: number;
  readonly elapsedSeconds: number;
  readonly movementSeconds: number;
  /** What the party's last committed turn did: `act`, `skip`, `wait`, or empty before one. */
  readonly last: string;
  readonly order: readonly TurnOrderView[];
}

/** The fight the party is in, as the product published it. */
export interface CombatView {
  readonly available: boolean;
  /** Whether anything is fighting the party right now. */
  readonly engaged: boolean;
  /** How many actors are fighting the party. */
  readonly opposition: number;
  /** How many of the party's members may act now. */
  readonly ready: number;
  readonly members: readonly FighterView[];
  readonly enemies: readonly FighterView[];
  readonly actor: string;
  /** How the last attack was made: `melee`, `ranged`, or `spell`; empty before any. */
  readonly kind: string;
  readonly target: string;
  readonly outcome: string;
  readonly code: string;
  readonly message: string;
  readonly recoverySeconds: number;
  readonly resolved: boolean;
  readonly hit: boolean;
  /** How likely the product said it was to land, in ten-thousandths. */
  readonly chance: number;
  readonly damageRolled: number;
  readonly damage: number;
  readonly damageKind: string;
  /** What the target resisted: `immune`, a weight, or empty when nothing was resolved. */
  readonly resistance: string;
  readonly condition: string;
  readonly targetDown: boolean;
  /** Whether the last order was the party's own rather than a blow it took. */
  readonly byParty: boolean;
  /** Which pacing the one fight is being played in: `realtime` or `turnbased`. */
  readonly pacing: string;
  readonly turn: TurnView;
}

function readFighter(f: Fields): FighterView {
  return {
    id: f.text('id'),
    name: f.text('name'),
    ready: f.flag('ready'),
    recoverySeconds: f.number('recoverySeconds'),
    distance: f.number('distance'),
    hitPoints: f.number('hitPoints'),
    hitPointsMax: f.number('hitPointsMax'),
    conditions: f.text('conditions'),
    down: f.flag('down'),
    activity: f.text('activity'),
  };
}

function readTurn(f: Fields): TurnView {
  return {
    phase: f.text('phase'),
    round: f.number('round'),
    actor: f.text('actor'),
    actorName: f.text('actorName'),
    playerTurn: f.flag('playerTurn'),
    dueSeconds: f.number('dueSeconds'),
    roundSeconds: f.number('roundSeconds'),
    elapsedSeconds: f.number('elapsedSeconds'),
    movementSeconds: f.number('movementSeconds'),
    last: f.text('last'),
    order: f.list('order', (entry) => ({
      id: entry.text('id'),
      name: entry.text('name'),
      side: entry.text('side'),
      remainingSeconds: entry.number('remainingSeconds'),
      ready: entry.flag('ready'),
      canAct: entry.flag('canAct'),
      waiting: entry.flag('waiting'),
      current: entry.flag('current'),
    })),
  };
}

export function readCombat(f: Fields): CombatView {
  return {
    available: f.flag('available'),
    engaged: f.flag('engaged'),
    opposition: f.number('opposition'),
    ready: f.number('ready'),
    members: f.list('members', readFighter),
    enemies: f.list('enemies', readFighter),
    actor: f.text('actor'),
    kind: f.text('kind'),
    target: f.text('target'),
    outcome: f.text('outcome', 'none'),
    code: f.text('code'),
    message: f.text('message'),
    recoverySeconds: f.number('recoverySeconds'),
    resolved: f.flag('resolved'),
    hit: f.flag('hit'),
    chance: f.number('chance'),
    damageRolled: f.number('damageRolled'),
    damage: f.number('damage'),
    damageKind: f.text('damageKind'),
    resistance: f.text('resistance'),
    condition: f.text('condition'),
    targetDown: f.flag('targetDown'),
    byParty: f.flag('byParty'),
    pacing: f.text('pacing', 'realtime'),
    turn: readTurn(f.object('turn')),
  };
}

/** What the fight section draws from: the fight, and its four controls as the product answered them. */
export interface CombatReading {
  readonly combat: CombatView;
  readonly attack: ControlView;
  readonly pace: ControlView;
  readonly skip: ControlView;
  readonly wait: ControlView;
}

/**
 * One fighter row: what it is called, what it has left to lose, what is acting on it, and whether it is out of the
 * fight. Being out of the fight outranks the recovery it still owes, so a laid-out actor reads as down rather than
 * as a ready light that is about to come on.
 */
function fighter(actor: FighterView, at = ''): HTMLLIElement {
  const row = element('li', 'crawler-fighter');
  row.dataset.ready = actor.ready ? 'yes' : 'no';
  row.dataset.fighter = actor.id;
  row.dataset.health = actor.hitPointsMax > 0 ? `${actor.hitPoints}/${actor.hitPointsMax}` : '';
  row.dataset.conditions = actor.conditions;
  row.dataset.down = actor.down ? 'yes' : 'no';
  row.dataset.activity = actor.activity;
  const word = actor.down ? 'down' : actor.ready ? 'ready' : `recovering ${actor.recoverySeconds.toFixed(1)}s`;
  const pools = actor.hitPointsMax > 0 ? ` — ${actor.hitPoints}/${actor.hitPointsMax} hp` : '';
  const conditions = actor.conditions === '' ? '' : ` — ${actor.conditions}`;
  // What a driven actor is doing is its own fact, printed beside the state unless it is the same word.
  const doing = actor.activity === '' || actor.activity === word ? '' : ` — ${actor.activity}`;
  row.textContent = `${actor.name} — ${word}${at}${pools}${conditions}${doing}`;
  return row;
}

/** Mounts the fight. */
export function mountCombat(host: Host): Section<CombatReading> {
  const { panel, claim } = host;
  const combat = section('crawler-combat');
  const state = element('p', 'crawler-combat-state');
  // Which pacing the one fight is played in, and the round it is in, printed from the product's own numbers.
  const turn = element('p', 'crawler-combat-turn');
  const actions = element('div', 'crawler-actions');
  const attack = button('Attack', 'crawler-attack');
  // Switching the pacing is its own control because it is its own act: the fight is the same fight either way.
  const pace = button('Turn-based', 'crawler-pace');
  // Skipping and waiting are separate controls because their consequences differ: a skipped turn forfeits the
  // round and owes the action it did not take, and a waited turn is deferred to the round's end.
  const skip = button('Skip', 'crawler-skip');
  const wait = button('Wait', 'crawler-wait');
  for (const control of [attack, pace, skip, wait]) {
    control.addEventListener('click', () => {
      if (control.dataset.action !== undefined && control.dataset.action !== '') claim(control.dataset.action);
    });
    actions.append(control);
  }

  const members = element('ul', 'crawler-combat-members');
  const enemies = element('ul', 'crawler-combat-enemies');
  // The order the round will act in, as the fight itself reads it.
  const order = element('ul', 'crawler-turn-order');
  const outcome = element('p', 'crawler-combat-result');
  outcome.hidden = true;
  combat.append(head('Fight'), state, turn, actions, members, enemies, order, outcome);

  const offer = (control: HTMLButtonElement, published: ControlView): void => {
    control.disabled = !published.enabled;
    control.dataset.action = published.action;
  };

  const render = (reading: CombatReading): void => {
    const view = reading.combat;
    panel.dataset.combat = !view.available ? 'none' : view.engaged ? 'engaged' : 'quiet';
    panel.dataset.combatReady = String(view.ready);
    panel.dataset.combatOpposition = String(view.opposition);
    panel.dataset.combatOutcome = view.outcome;
    combat.hidden = !view.available;
    state.textContent = !view.available
      ? ''
      : view.engaged
        ? `Engaged with ${view.opposition} — ${view.ready} of ${view.members.length} ready`
        : `Nobody is hostile — ${view.ready} of ${view.members.length} ready`;
    members.replaceChildren(...view.members.map((member) => fighter(member)));
    enemies.replaceChildren(...view.enemies.map((enemy) => fighter(enemy, ` at ${enemy.distance.toFixed(0)}`)));

    // The pacing, the round, and what the actor whose turn it is still owes: a player pressing the act key needs to
    // know whether the world is waiting for them, and nothing here works that out for itself.
    const paced = view.pacing === 'turnbased';
    panel.dataset.pacing = view.available ? view.pacing : 'none';
    panel.dataset.turnPhase = view.turn.phase;
    panel.dataset.turnRound = String(view.turn.round);
    panel.dataset.turnActor = view.turn.actorName;
    panel.dataset.turnPlayer = view.turn.playerTurn ? 'yes' : 'no';
    panel.dataset.turnLast = view.turn.last;
    turn.textContent = !view.available
      ? ''
      : !paced
        ? 'Real time: actors act as their own recovery elapses.'
        : view.turn.phase === 'movement'
          ? `Move: round ${view.turn.round}, ${view.turn.movementSeconds.toFixed(1)}s of movement left — any turn control ends it.`
          : view.turn.phase === 'action'
            ? `Round ${view.turn.round} — ${view.turn.actorName}'s turn${view.turn.playerTurn ? ' (yours)' : ''}${
                view.turn.dueSeconds > 0 ? `, due in ${view.turn.dueSeconds.toFixed(1)}s` : ''
              }${view.turn.roundSeconds > 0 ? ` · ${view.turn.roundSeconds.toFixed(1)}s round` : ''}${
                view.turn.last === '' ? '' : ` · last turn ${view.turn.last}`
              }`
            : 'Turn-based: nothing is being fought, so no round is under way.';
    order.replaceChildren(
      ...view.turn.order.map((actor) => {
        const row = element('li', 'crawler-turn-actor');
        row.dataset.turnActor = actor.id;
        row.dataset.side = actor.side;
        row.dataset.ready = actor.ready ? 'yes' : 'no';
        row.dataset.current = actor.current ? 'yes' : 'no';
        row.dataset.waiting = actor.waiting ? 'yes' : 'no';
        row.dataset.remaining = actor.remainingSeconds.toFixed(3);
        const owed = !actor.canAct ? ' — out of the fight' : actor.ready ? ' — ready' : ` — ${actor.remainingSeconds.toFixed(1)}s`;
        row.textContent = `${actor.name}${owed}${actor.current ? ' — this turn' : ''}${actor.waiting ? ' — waiting' : ''}`;
        return row;
      }),
    );

    // Each control is offered exactly when the product says it would take it: the act control, the pacing, and the
    // two turn actions each carry the product's own answer.
    offer(attack, reading.attack);
    offer(pace, reading.pace);
    offer(skip, reading.skip);
    offer(wait, reading.wait);
    pace.textContent = paced ? 'Real-time' : 'Turn-based';

    // What the last attack came to, in the product's own numbers, and whose blow it was: a wound the party took
    // must not read as the party's own doing.
    outcome.hidden = view.message === '';
    outcome.dataset.outcome = view.outcome;
    outcome.dataset.code = view.code;
    outcome.dataset.resolved = view.resolved ? 'yes' : 'no';
    outcome.dataset.hit = view.hit ? 'yes' : 'no';
    outcome.dataset.chance = String(view.chance);
    outcome.dataset.damage = String(view.damage);
    outcome.dataset.damageRolled = String(view.damageRolled);
    outcome.dataset.damageKind = view.damageKind;
    outcome.dataset.resistance = view.resistance;
    outcome.dataset.condition = view.condition;
    outcome.dataset.targetDown = view.targetDown ? 'yes' : 'no';
    outcome.dataset.byParty = view.byParty ? 'yes' : 'no';
    outcome.textContent = view.message;
  };

  return { element: combat, render };
}
