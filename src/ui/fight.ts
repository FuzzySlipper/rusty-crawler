/**
 * The fight beside the world: which pacing it is played in and whose turn it is, who is acting and whether they may,
 * what their attack would strike, the controls that order it, the nearest foes, and the fight's last blow.
 *
 * It is the player's panel for a fight in progress, sized to sit beside the world without scrolling: the foes are the
 * nearest few, as the product lists them nearest first, with the rest counted. Every fact is the product's — the
 * acting member is the roster's selection, the target is the fight's own answer for that member's attack, a recovery
 * is the fight's own reading — and each control is offered exactly when the product says it would be taken. A member is
 * chosen by their portrait along the bottom, which the selection line answers. The complete fight, every actor and the
 * round's whole order, stays in the diagnostic panel.
 */

import type { CombatReading, FighterView } from './combat.js';
import type { ControlView } from './overview.js';
import { button, element, section, type Host, type Section } from './dom.js';

/** How many foes the panel lists before it counts the rest. */
const SHOWN_FOES = 5;

/** A foe's row: its name, how far off, what it has left, and what it is doing. */
function foe(enemy: FighterView, aimed: boolean): HTMLLIElement {
  const row = element('li', 'crawler-fight-foe');
  row.dataset.fighter = enemy.id;
  row.dataset.aim = aimed ? 'yes' : 'no';
  row.dataset.down = enemy.down ? 'yes' : 'no';
  const name = element('span', 'crawler-fight-foe-name');
  name.textContent = enemy.name;
  const facts = element('span', 'crawler-fight-foe-facts');
  const health = enemy.hitPointsMax > 0 ? ` · ${enemy.hitPoints}/${enemy.hitPointsMax} hp` : '';
  const doing = enemy.down ? ' · down' : enemy.activity === '' ? '' : ` · ${enemy.activity}`;
  facts.textContent = `${enemy.distance.toFixed(0)} away${health}${doing}`;
  row.append(name, facts);
  return row;
}

/** Mounts the fight beside the world. */
export function mountFight(host: Host): Section<CombatReading> {
  const { claim } = host;
  const panel = section('crawler-fight-panel');
  const pace = element('p', 'crawler-fight-pace');
  const actor = element('p', 'crawler-fight-actor');
  const selection = element('p', 'crawler-fight-selection');
  const target = element('p', 'crawler-fight-target');
  const controls = element('div', 'crawler-fight-controls');
  const made = new Map<string, { button: HTMLButtonElement; label: string }>();
  for (const [key, label] of [['attack', 'Attack'], ['next', 'Next member'], ['pace', 'Turn-based'], ['skip', 'Skip turn'], ['wait', 'Wait']] as const) {
    const control = button(label, `crawler-fight-${key}`);
    control.addEventListener('click', () => {
      if (control.dataset.action !== undefined && control.dataset.action !== '') claim(control.dataset.action);
    });
    controls.append(control);
    made.set(key, { button: control, label });
  }

  const foes = element('ul', 'crawler-fight-foes');
  const more = element('p', 'crawler-fight-more');
  const last = element('p', 'crawler-fight-last');
  panel.append(pace, actor, selection, target, controls, foes, more, last);

  const offer = (key: string, published: ControlView, label?: string): void => {
    const control = made.get(key)!;
    control.button.disabled = !published.enabled;
    control.button.dataset.action = published.action;
    const text = label ?? control.label;
    control.button.textContent = published.key === '' ? text : `${text} (${published.key})`;
  };

  const render = (reading: CombatReading): void => {
    const view = reading.combat;
    panel.hidden = !view.available;
    const paced = view.pacing === 'turnbased';
    const { turn } = view;
    pace.dataset.pacing = view.pacing;
    pace.textContent = !paced
      ? 'Real time — everyone acts as their recovery allows'
      : turn.phase === 'movement'
        ? `Turn-based · round ${turn.round} · moving, ${turn.movementSeconds.toFixed(1)}s left`
        : turn.phase === 'action'
          ? `Turn-based · round ${turn.round} · ${turn.actorName}'s turn${turn.playerTurn ? ' (yours)' : ''}`
          : 'Turn-based · no round under way';

    // The acting member is the roster's selection, read with the fight's own answer about them.
    const acting = view.members.find((member) => member.selected);
    actor.dataset.state = acting === undefined ? 'none' : acting.down ? 'down' : acting.ready ? 'ready' : 'recovering';
    actor.textContent = acting === undefined
      ? 'Nobody is selected to act.'
      : `Acting: ${acting.name} — ${acting.down ? `cannot act${acting.conditions === '' ? '' : ` (${acting.conditions})`}` : acting.ready ? 'ready' : `recovering ${acting.recoverySeconds.toFixed(1)}s`}`;
    selection.hidden = view.selectionMessage === '';
    selection.textContent = view.selectionMessage;
    selection.dataset.code = view.selectionCode;

    const aimed = view.enemies.find((enemy) => enemy.id === view.aim);
    target.dataset.aim = view.aim;
    target.textContent = aimed !== undefined
      ? `Target: ${aimed.name} · ${aimed.distance.toFixed(0)} away${aimed.hitPointsMax > 0 ? ` · ${aimed.hitPoints}/${aimed.hitPointsMax} hp` : ''}`
      : view.aimName !== ''
        ? `Target: ${view.aimName}`
        : acting === undefined
          ? 'Target: none'
          : `Target: nothing within ${acting.name}'s reach`;

    offer('attack', reading.attack);
    offer('next', reading.nextMember);
    offer('pace', reading.pace, paced ? 'Real-time' : 'Turn-based');
    offer('skip', reading.skip);
    offer('wait', reading.wait);
    made.get('skip')!.button.hidden = !paced;
    made.get('wait')!.button.hidden = !paced;

    // The nearest few, as the product lists them nearest first; the rest are counted, not drawn.
    foes.replaceChildren(...view.enemies.slice(0, SHOWN_FOES).map((enemy) => foe(enemy, enemy.id === view.aim)));
    const hidden = view.enemies.length - SHOWN_FOES;
    more.hidden = hidden <= 0;
    more.textContent = hidden > 0 ? `and ${hidden} more` : '';

    // The fight's last blow, whoever struck it: a wound the party took reads as the creature's, not the party's.
    last.hidden = view.message === '';
    last.dataset.byParty = view.byParty ? 'yes' : 'no';
    last.dataset.outcome = view.outcome;
    last.textContent = view.message === '' ? '' : `${view.byParty ? 'Last order' : 'Last blow'}: ${view.message}`;
  };

  return { element: panel, render };
}
