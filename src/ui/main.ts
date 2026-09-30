/**
 * The product's DOM companion: it renders the session projection and reports semantic actions.
 *
 * It owns no state, evaluates no rules, and starts no loop or timer. Every value it shows arrived in the last
 * projection from the product, every control it offers is offered exactly when the product says it would be taken,
 * and every action it sends is an action name the product defines. When the projection says the session is held,
 * the pause control offers to release it; when it says a party is being created, the screen offers exactly the
 * choices that projection listed and shows the rule the flow answered with when one of them was refused. Nothing
 * here decides on its own that a choice is illegal — the refusal is the product's answer, and a screen that hid it
 * would be hiding the game's rules.
 *
 * This module only composes the panel: each block is read by its own module and drawn by its own section, and every
 * reader and every section is a named export the companion suite can call on its own.
 */

import { mountAlchemy } from './alchemy.js';
import { UI_ACTION_CONTRACT, UI_ACTION_INTENT, UI_CONTRACT, type Claim } from './actions.js';
import { mountCombat } from './combat.js';
import type { ProductUiContext, ProjectionEnvelope } from './context.js';
import { mountConversation } from './conversation.js';
import { mountCreation } from './creation.js';
import { mountDetails } from './details.js';
import { element, type Host } from './dom.js';
import { mountJournal } from './journal.js';
import { mountMap } from './map.js';
import { mountProgression } from './progression.js';
import { mountPromotion } from './promotion.js';
import { mountQuests } from './quests.js';
import { mountRest } from './rest.js';
import { mountService } from './service.js';
import { mountSkills } from './skills.js';
import { readSnapshot, type SnapshotView } from './snapshot.js';
import { mountSpellbook } from './spellbook.js';
import { STYLES } from './styles.js';

export type { ProductUiContext } from './context.js';
export { readSnapshot } from './snapshot.js';

/**
 * Mounts the companion into `root` and returns a disposer. The returned object holds the only subscription this
 * module creates; disposing it leaves no listener behind.
 */
export function mountProductUi(root: HTMLElement, context: ProductUiContext): { dispose(): void } {
  const style = element('style');
  style.textContent = STYLES;
  const panel = element('section', 'crawler-session');

  // Nothing has been published yet, so every attribute says the panel does not know rather than that nothing
  // happened: no movement facts, no clock, no party, and no counter.
  Object.assign(panel.dataset, {
    mode: 'starting',
    projection: 'none',
    problems: '0',
    motion: 'none',
    blocked: 'none',
    clock: 'none',
    party: 'none',
    light: 'unknown',
    creation: 'none',
    save: 'none',
    interaction: 'none',
    use: 'none',
    service: 'none',
    serviceState: 'unknown',
    serviceAction: '',
    serviceOutcome: 'none',
    conversation: 'none',
    conversationAction: '',
    conversationOutcome: 'none',
    progression: 'none',
    progressionOutcome: 'none',
    rest: 'none',
    restAction: '',
    restOutcome: 'none',
    tired: 'no',
  });

  const claim: Claim = (action, data = {}) => {
    context.intents?.claim(UI_ACTION_INTENT, {
      kind: 'product-payload',
      contract: UI_ACTION_CONTRACT,
      data: { ...data, action },
    });
    // Every claim here comes from a control the player clicked, which now holds keyboard focus; the Engine gives
    // the game no key whose event path runs through a button, so the game view takes focus back and the next held
    // key walks the party rather than pressing the button again.
    context.ui.focusGameplay();
  };
  const host: Host = { panel, claim };

  const details = mountDetails(host);
  const creation = mountCreation(host);
  const conversation = mountConversation(host);
  const service = mountService(host);
  const rest = mountRest(host);
  const combat = mountCombat(host);
  const progression = mountProgression(host);
  const promotion = mountPromotion(host);
  const skills = mountSkills(host);
  const spellbook = mountSpellbook(host);
  const alchemy = mountAlchemy(host);
  const map = mountMap(host);
  const journal = mountJournal(host);
  const quests = mountQuests(host);
  journal.hold(quests.element);

  // What the panel could not read of the last projection, by field: a contract the product broke is named here
  // rather than shown as an empty section that looks like a quiet session.
  const problems = element('ul', 'crawler-problems');
  problems.hidden = true;

  // The screens a player acts on come before the long list of facts: while a party is being made its choices are
  // what a player acts on, and a counter's shelves, a conversation, and a fight are the same.
  panel.append(
    ...details.top,
    creation.element,
    conversation.element,
    service.element,
    rest.element,
    combat.element,
    progression.element,
    promotion.element,
    skills.element,
    spellbook.element,
    alchemy.element,
    details.awards,
    map.element,
    journal.element,
    ...details.bottom,
    problems,
  );
  root.append(style, panel);

  const render = (snapshot: SnapshotView): void => {
    const { controls } = snapshot;
    details.render(snapshot);
    creation.render({
      creation: snapshot.creation,
      resumed: snapshot.save.resumed,
      advance: controls.creationAdvance,
      accept: controls.creationAccept,
    });
    conversation.render({ conversation: snapshot.conversation, leave: controls.conversationLeave });
    service.render({ service: snapshot.service, leave: controls.serviceLeave });
    rest.render({
      rest: snapshot.rest,
      controls: [controls.rest, controls.camp, controls.waitDawn, controls.waitHour, controls.waitFiveMinutes],
    });
    combat.render({
      combat: snapshot.combat,
      attack: controls.attack,
      pace: controls.turnBased,
      skip: controls.turnSkip,
      wait: controls.turnWait,
    });
    progression.render(snapshot.progression);
    promotion.render(snapshot.promotion);
    skills.render(snapshot.skills);
    spellbook.render(snapshot.magic);
    alchemy.render(snapshot.alchemy);
    map.render(snapshot.map);
    // The books first, so the quests book's own title and state sentence are the product's words, and then the
    // quests book draws its errands underneath them.
    journal.render(snapshot.journal);
    quests.render(
      { quests: snapshot.quests, book: snapshot.journal.books.find((book) => book.kind === 'quests') },
      journal.element,
    );
  };

  const receive = (projection: ProjectionEnvelope | null): void => {
    // No envelope is the Engine saying there is no projection now — before the product first publishes, or while
    // its runtime is rebound — and the panel says so rather than presenting the last one as current.
    if (projection === null) {
      panel.dataset.projection = 'none';
      return;
    }

    // A projection on another contract is not this companion's to interpret.
    if (projection.contract !== UI_CONTRACT) return;
    panel.dataset.projection = 'current';
    const reading = readSnapshot(projection.value);
    panel.dataset.problems = String(reading.problems.length);
    problems.hidden = reading.problems.length === 0;
    problems.replaceChildren(
      ...reading.problems.map((problem) => {
        const item = element('li');
        item.textContent = problem;
        return item;
      }),
    );
    if (reading.snapshot !== null) render(reading.snapshot);
  };

  // The Engine delivers the current envelope before `subscribe` returns, so a panel mounted after the product has
  // published renders at once rather than waiting for the next update.
  const unsubscribe = context.projection?.subscribe(receive);

  return {
    dispose(): void {
      unsubscribe?.();
      panel.remove();
      style.remove();
    },
  };
}
