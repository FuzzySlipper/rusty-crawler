/**
 * The panel's own head and rows: the composition and the place, the long list of the session's facts, the three
 * session controls (pause, save, use) with what the last save and use did, the party's accomplishments, and the
 * keyboard hint.
 *
 * Every row prints a value the projection carried and every control follows the answer the product published for
 * it: whether it is offered, what it sends, and the key it is bound to. Nothing here decides that a control would
 * work, and nothing here names a key the host did not bind.
 */

import type { ControlView, PartyView } from './overview.js';
import type { SnapshotView } from './snapshot.js';
import { ACTIONS } from './actions.js';
import { button, element, head, plural, result, section, type Host } from './dom.js';

/** The rows of the fact list, in the order a person reads them, each with its label. */
const ROWS = [
  ['mode', 'Session'],
  ['simulation', 'Simulation'],
  ['steps', 'Admitted steps'],
  ['content', 'Content'],
  ['date', 'Date'],
  ['time', 'Time'],
  ['days', 'Days'],
  ['party', 'Party'],
  ['pack', 'Pack'],
  ['coins', 'Coins'],
  ['owed', 'Owed'],
  ['food', 'Food'],
  ['standing', 'Standing'],
  ['regard', 'Regard'],
  ['condition', 'Condition'],
  ['vitality', 'Vitality'],
  ['magic', 'Magic'],
  ['place', 'Place'],
  ['hours', 'Hours'],
  ['pose', 'Position'],
  ['explored', 'Explored'],
  ['motion', 'Motion'],
  ['blocked', 'Blocked'],
  ['step', 'Step up'],
  ['fall', 'Fall'],
  ['footing', 'Footing'],
  ['spared', 'Spared by'],
  ['member', 'Member'],
  ['creationStep', 'Creation step'],
  ['pool', 'Pool'],
  ['save', 'Save'],
  ['start', 'Start'],
  ['facing', 'Facing'],
  ['bodies', 'Bodies here'],
  ['requires', 'Requires'],
  ['use', 'Use'],
] as const;

type RowKey = (typeof ROWS)[number][0];

/** The mounted head, rows, and session controls, each a named part the panel places. */
export interface Details {
  /** The title, the ruleset, the bundle, and the place, which head the panel. */
  readonly top: readonly HTMLElement[];
  /** The fact list, the session controls, their results, and the hint, which close the panel. */
  readonly bottom: readonly HTMLElement[];
  /** The accomplishments section, which the panel places among the other sections. */
  readonly awards: HTMLElement;
  render(snapshot: SnapshotView): void;
}

/** A key's clause for a hint, empty when the host bound the control to no key. */
function withKey(key: string, lead = ', or with the '): string {
  return key === '' ? '' : `${lead}${key} key`;
}

/** Lists keys for a person: `T`, `T or H`, `T, H, or M`. */
function keyList(keys: readonly string[]): string {
  const bound = keys.filter((key) => key !== '');
  if (bound.length <= 1) return bound.join('');
  if (bound.length === 2) return `${bound[0]} or ${bound[1]}`;
  return `${bound.slice(0, -1).join(', ')}, or ${bound[bound.length - 1]}`;
}

/** Offers a control exactly as the product published it, and remembers what a press sends on the button itself. */
function offer(control: HTMLButtonElement, published: ControlView): void {
  control.disabled = !published.enabled;
  control.dataset.action = published.action;
}

/** Mounts the head, the fact rows, the session controls, and the accomplishments. */
export function mountDetails(host: Host): Details {
  const { panel, claim } = host;
  const title = element('h1');
  const ruleset = element('p', 'crawler-ruleset');
  const bundle = element('p', 'crawler-bundle');
  const place = element('p', 'crawler-place');

  const details = element('dl');
  const rows = {} as Record<RowKey, HTMLElement>;
  for (const [key, label] of ROWS) {
    const term = element('dt');
    term.textContent = label;
    const value = element('dd');
    value.textContent = '—';
    details.append(term, value);
    rows[key] = value;
  }

  // The pause control: one button that holds or releases the session, whichever the product says a press would do.
  const action = button('Starting…');
  action.disabled = true;

  // The save control, and the answer the last request got: a save that could not land must not look like one that
  // did, so the result line prints the product's own sentence.
  const saveButton = button('Save session', 'crawler-save');
  saveButton.disabled = true;
  const saveResult = result('crawler-save-result');

  // The use control, and what the last use did and could not deliver; the reason the reticle holds nothing is on the
  // Facing row beside it.
  const useButton = button('Use', 'crawler-use');
  useButton.disabled = true;
  const useResult = result('crawler-use-result');
  const useResidue = result('crawler-use-residue');

  const hint = element('p', 'crawler-hint');
  const companions = section('crawler-followers');
  const companionsList = element('div', 'crawler-followers-list');
  companions.append(head('Companions'), companionsList);
  hint.textContent = 'Pause or resume with the button.';

  for (const control of [action, saveButton, useButton]) {
    control.addEventListener('click', () => {
      if (control.dataset.action !== undefined && control.dataset.action !== '') claim(control.dataset.action);
    });
  }

  // What the party has accomplished: one row per record this game counts, in the game's own words, and nothing
  // at all when the game counts nothing. The panel groups nothing, sorts nothing, and decides nothing.
  const awards = section('crawler-awards');
  const awardsState = element('p', 'crawler-awards-state');
  const awardsList = element('div', 'crawler-awards-list');
  awards.append(head('Accomplishments'), awardsState, awardsList);

  const renderAwards = (party: PartyView): void => {
    awards.hidden = !party.present;
    awardsState.textContent = !party.present
      ? ''
      : !party.standingRead
        ? 'This game keeps no account of what the party has done.'
        : party.awards.length === 0
          ? 'Nothing the party has done is on record yet.'
          : `${party.awards.length} thing${plural(party.awards.length)} on record`;
    awardsList.replaceChildren(
      ...(party.present ? party.awards : []).map((award) => {
        const row = element('div', 'crawler-award');
        row.dataset.award = award.id;
        row.dataset.kind = award.kind;
        const label = element('span', 'crawler-row-label');
        label.textContent = award.detail === '' ? `${award.label} · ${award.kind}` : `${award.label} · ${award.kind} · ${award.detail}`;
        row.append(label);
        return row;
      }),
    );
  };

  const render = (snapshot: SnapshotView): void => {
    const { composition, session, world, movement, clock, party, save, interaction, creation, controls } = snapshot;
    const mode = session.mode;
    title.textContent = 'Rusty Crawler';
    ruleset.textContent = composition.title;
    bundle.textContent =
      composition.bundle === ''
        ? 'No game bundle selected'
        : `${composition.bundle} · ${composition.contentPacks} pack${plural(composition.contentPacks)}`;
    place.textContent = world.places === 0 ? 'No world loaded' : `${world.name}${world.kind === '' ? '' : ` · ${world.kind}`}`;
    panel.dataset.mode = mode;
    panel.dataset.ruleset = composition.ruleset;
    panel.dataset.bundle = composition.bundle;
    panel.dataset.place = world.place;

    rows.mode.textContent = mode;
    rows.simulation.textContent = `${session.simulationSeconds.toFixed(1)} s`;
    rows.steps.textContent = String(session.admittedSteps);
    rows.content.textContent = String(composition.contentPacks);

    // The clock's own facts, printed as they arrived. `present` is what tells a session whose ruleset composed no
    // clock from one standing on the first day of its calendar, and the two must not look alike.
    panel.dataset.clock = clock.present ? 'present' : 'none';
    panel.dataset.light = clock.present ? clock.daylight : 'unknown';
    rows.date.textContent = clock.present ? clock.date : '—';
    rows.time.textContent = clock.present ? `${clock.time} · ${clock.daylight}` : '—';
    rows.days.textContent = clock.present ? String(clock.elapsedDays) : '—';

    // The party's accounts and standing, read from the party the product holds and never counted here. A product
    // that names no band shows the numbers alone rather than a word invented for a threshold this panel does not own.
    panel.dataset.party = party.present ? 'present' : 'none';
    rows.party.textContent = party.present ? String(party.members) : '—';
    companions.hidden = !party.present || party.followers.length === 0;
    companionsList.replaceChildren(...party.followers.map((follower) => {
      const talk = button(`${follower.name} · ${follower.kind}${follower.portrait === '' ? '' : ` · portrait ${follower.portrait}`}`);
      talk.disabled = !follower.canTalk;
      talk.dataset.action = follower.talkAction;
      talk.dataset.follower = follower.id;
      talk.dataset.portrait = follower.portrait;
      talk.addEventListener('click', () => claim(follower.talkAction, { target: follower.id }));
      const row = element('div');
      const benefits = element('span');
      benefits.textContent = follower.benefits === '' ? '' : ` · ${follower.benefits}`;
      row.append(talk, benefits);
      return row;
    }));
    rows.pack.textContent = party.present ? String(party.pack) : '—';
    rows.coins.textContent = party.present ? String(party.coins) : '—';
    // What the party owes, account by account, as the product published it: owing is not paying, so a fine the
    // purse could not cover is shown beside the purse rather than hidden in it.
    rows.owed.textContent =
      party.present && party.debts.length > 0 ? party.debts.map((debt) => `${debt.coins} (${debt.account})`).join(', ') : '—';
    rows.food.textContent = party.present ? `${party.provisions} ${party.unit}` : '—';
    rows.standing.textContent = !party.present
      ? '—'
      : party.standing === ''
        ? `${party.reputation} / ${party.fame}`
        : `${party.standing} · ${party.reputation} / ${party.fame}`;
    rows.regard.textContent = party.present && party.standingDetail !== '' ? party.standingDetail : '—';
    rows.condition.textContent = party.present && party.conditions !== '' ? party.conditions : '—';
    rows.vitality.textContent = party.present ? `${party.hitPoints} / ${party.hitPointsMax}` : '—';
    rows.magic.textContent = party.present ? `${party.spellPoints} / ${party.spellPointsMax}` : '—';
    renderAwards(party);

    // The place and the party's pose in it, and the hours the place keeps as the product's own clock read.
    rows.place.textContent = world.place === '' ? '—' : world.place;
    rows.hours.textContent =
      world.hours === ''
        ? '—'
        : `${world.hours} · ${world.open ? 'open' : 'closed'}${world.nextChange === '' ? '' : ` · next ${world.nextChange}`}`;
    rows.pose.textContent =
      world.places === 0 ? '—' : `${world.x.toFixed(0)}, ${world.y.toFixed(0)}, ${world.z.toFixed(0)} @ ${world.yaw.toFixed(0)}`;
    rows.explored.textContent = `${world.visited} / ${world.places}`;

    // A session with no movement facts shows that it does not know what the last step did: showing a clear path
    // there would leave a refused step and an input that never arrived looking the same again.
    panel.dataset.motion = movement.motion;
    panel.dataset.blocked = movement.blocked;
    rows.motion.textContent = movement.motion === 'none' ? '—' : movement.motion;
    rows.blocked.textContent = movement.blocked === 'none' ? '—' : movement.blocked;
    rows.step.textContent = movement.stepRise > 0 ? `+${movement.stepRise.toFixed(1)}` : '—';
    rows.fall.textContent =
      movement.fallDistance > 0 ? `${movement.fallDistance.toFixed(0)} · ${movement.fallDamage.toFixed(0)} damage` : '—';

    // The ground under the party and what spares whom, as the product read them: the panel prints the interval and
    // the time before the next harm it was handed, and decides nothing about who is harmed.
    const { footing } = movement;
    panel.dataset.footing = footing.ground === '' ? 'none' : footing.ground;
    panel.dataset.harm = footing.harmful ? 'harmful' : 'none';
    rows.footing.textContent =
      footing.ground === ''
        ? '—'
        : footing.harmful
          ? `${footing.ground} · harms every ${footing.every.toFixed(0)} s · next in ${footing.nextHarmIn.toFixed(0)} s`
          : footing.ground;
    rows.spared.textContent =
      footing.shelters.length === 0
        ? '—'
        : footing.shelters
            .map((shelter) => `${shelter.name} (${shelter.memberName}${shelter.everybody ? ', everybody' : ''})`)
            .join(', ');

    // Creation's own facts: which member is being made, where it stands, and what the pool still holds.
    rows.member.textContent = creation.active || creation.accepted ? `${creation.member + 1} / ${creation.members}` : '—';
    rows.creationStep.textContent = creation.step === '' ? '—' : creation.step;
    rows.pool.textContent = creation.active ? String(creation.pool) : '—';

    // The save's own facts: whether this session can save at all, whether it came from the slot, and what
    // happened the last time the player asked.
    panel.dataset.save = save.state;
    rows.save.textContent = !save.available
      ? 'unavailable'
      : save.state === 'saved'
        ? `${save.at} · ${save.slot}`
        : save.state === 'failed'
          ? 'failed'
          : '—';
    // Which start the party took is the composition's own word: a new party created on the screen, a new party
    // the scenario fixed, or the one a save held.
    panel.dataset.partyStart = composition.partyStart;
    rows.start.textContent =
      composition.partyStart === 'creation'
        ? 'new · created party'
        : composition.partyStart === 'scenario'
          ? 'new · scenario party'
          : composition.partyStart === 'resumed'
            ? 'resumed'
            : '—';
    saveResult.hidden = save.message === '';
    saveResult.dataset.state = save.state;
    saveResult.dataset.code = save.code;
    saveResult.textContent = save.message;

    // The interaction's own facts: what the party faces, what it requires, and what the last use did.
    panel.dataset.interaction = interaction.available ? interaction.reason : 'none';
    panel.dataset.bodies = String(interaction.bodies);
    panel.dataset.use = interaction.outcome;
    rows.facing.textContent = !interaction.available
      ? '—'
      : interaction.label === ''
        ? interaction.reason
        : `${interaction.label} · ${interaction.verb}${interaction.state === '' ? '' : ` · ${interaction.state}`} · ${interaction.distance.toFixed(0)}`;
    rows.bodies.textContent = interaction.available ? String(interaction.bodies) : '—';
    rows.requires.textContent = interaction.requires.length === 0 ? '—' : interaction.requires.join(', ');
    rows.use.textContent = interaction.outcome === 'none' ? '—' : interaction.outcome;
    useResult.hidden = interaction.message === '';
    useResult.dataset.outcome = interaction.outcome;
    useResult.dataset.code = interaction.code;
    useResult.textContent = interaction.message;
    useResidue.hidden = interaction.residue === '';
    useResidue.textContent = interaction.residue;

    // The three session controls follow the product's answers: whether a press would be taken, and what it sends.
    offer(action, controls.pause);
    action.textContent = controls.pause.enabled
      ? controls.pause.action === ACTIONS.resume
        ? 'Resume session'
        : 'Pause session'
      : mode === 'creating'
        ? 'Creating a party…'
        : mode === 'stopped'
          ? 'Session stopped'
          : 'Starting…';
    offer(saveButton, controls.save);
    offer(useButton, controls.use);

    // The hint names the keys the host bound, and only those: a control bound to no key is named by its button.
    const rest = snapshot.rest.available
      ? ` Rest with the button${withKey(controls.rest.key, ' or the ')}${
          controls.camp.key === '' ? '' : `; camp with ${controls.camp.key}`
        }${
          keyList([controls.waitDawn.key, controls.waitHour.key, controls.waitFiveMinutes.key]) === ''
            ? ''
            : `; wait with ${keyList([controls.waitDawn.key, controls.waitHour.key, controls.waitFiveMinutes.key])}`
        }.`
      : '';
    const fight = snapshot.combat.available
      ? ` Attack with the button${withKey(controls.attack.key, ' or the ')}.${
          snapshot.combat.pacing === 'turnbased'
            ? ` Switch the pacing with the button${withKey(controls.turnBased.key, ' or the ')}${
                controls.turnSkip.key === '' ? '' : `; skip a turn with ${controls.turnSkip.key}`
              }${controls.turnWait.key === '' ? '' : `; wait with ${controls.turnWait.key}`}.`
            : ` Switch to turn-based pacing with the button${withKey(controls.turnBased.key, ' or the ')}.`
        }`
      : '';
    hint.textContent =
      mode === 'creating'
        ? `Choose a portrait, a class, a name, attributes, and skills.${
            controls.creationAdvance.key === '' ? '' : ` ${controls.creationAdvance.key} confirms the step you are on;`
          }${controls.creationAccept.key === '' ? '' : ` ${controls.creationAccept.key} accepts a finished party.`}`
        : [
            `Pause or resume with the button${withKey(controls.pause.key)}.`,
            save.available ? ` Save with the button${withKey(controls.save.key)}.` : '',
            interaction.available ? ` Use with the button${withKey(controls.use.key)}.` : '',
            snapshot.service.open ? ` Leave the counter with the button${withKey(controls.serviceLeave.key)}.` : '',
            rest,
            fight,
            snapshot.magic.available ? ' Cast from the spellbook rows.' : '',
          ].join('');
  };

  return {
    top: [title, ruleset, bundle, place],
    bottom: [details, companions, action, saveButton, useButton, saveResult, useResult, useResidue, hint],
    awards,
    render,
  };
}
