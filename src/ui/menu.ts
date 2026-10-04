/**
 * The product's title and lifecycle menu. It renders the Host's menu projection and sends only the semantic
 * actions the Host declared; it owns no session, save, or confirmation state of its own.
 */

import { ACTIONS } from './actions.js';
import { button, element, type Host } from './dom.js';
import type { Fields } from './reader.js';

/** The lifecycle menu state published beside the adventure projection. */
export interface MenuView {
  readonly visible: boolean;
  readonly screen: 'adventure' | 'title' | 'confirm-return' | 'save-load' | 'confirm-overwrite' | 'confirm-load';
  readonly canNewGame: boolean;
  readonly canContinue: boolean;
  readonly canReturnTitle: boolean;
  readonly hasUnsaved: boolean;
  readonly state: string;
  readonly code: string;
  readonly message: string;
  readonly save: SaveMenuView;
}

/** The one save slot's context as the explicit save/load screen presents it. */
export interface SaveMenuView {
  readonly available: boolean;
  readonly present: boolean;
  readonly slot: string;
  readonly party: string;
  readonly members: number;
  readonly coins: number;
  readonly provisions: number;
  readonly place: string;
  readonly calendar: string;
  readonly savedAt: string;
  readonly state: string;
  readonly code: string;
  readonly message: string;
}

function readSaveMenu(f: Fields): SaveMenuView {
  return {
    available: f.flag('available'),
    present: f.flag('present'),
    slot: f.text('slot', 'session'),
    party: f.text('party'),
    members: f.number('members'),
    coins: f.number('coins'),
    provisions: f.number('provisions'),
    place: f.text('place'),
    calendar: f.text('calendar'),
    savedAt: f.text('savedAt'),
    state: f.text('state', 'none'),
    code: f.text('code'),
    message: f.text('message'),
  };
}

/** Reads the menu block exactly as the Host/session projection published it. */
export function readMenu(f: Fields): MenuView {
  const screen = f.text('screen', 'adventure');
  return {
    visible: f.flag('visible'),
    screen: screen === 'title' || screen === 'confirm-return' || screen === 'save-load' ||
      screen === 'confirm-overwrite' || screen === 'confirm-load' ? screen : 'adventure',
    canNewGame: f.flag('canNewGame'),
    canContinue: f.flag('canContinue'),
    canReturnTitle: f.flag('canReturnTitle'),
    hasUnsaved: f.flag('hasUnsaved'),
    state: f.text('state', 'none'),
    code: f.text('code'),
    message: f.text('message'),
    save: readSaveMenu(f.object('save')),
  };
}

/** The mounted menu and its projection-driven render operation. */
export interface Menu {
  readonly element: HTMLElement;
  render(view: MenuView, title: string, subtitle: string): void;
  clear(): void;
}

/** Mounts the title, explicit save/load, continuation, and leave-unsaved controls. */
export function mountMenu(host: Host): Menu {
  const root = element('section', 'crawler-menu');
  const launch = button('Menu', 'crawler-menu-launch');
  const overlay = element('div', 'crawler-menu-overlay');
  const card = element('div', 'crawler-menu-card');
  const heading = element('h2', 'crawler-menu-title');
  const subtitle = element('p', 'crawler-menu-subtitle');
  const state = element('p', 'crawler-menu-state');
  const message = element('p', 'crawler-menu-message');
  const titleActions = element('div', 'crawler-menu-title-actions crawler-menu-actions');
  const newGame = button('New Game', 'crawler-menu-new-game');
  const continueGame = button('Continue', 'crawler-menu-continue');
  const saveLoad = element('section', 'crawler-menu-save-load');
  const saveSummary = element('div', 'crawler-menu-save-summary');
  const saveSlot = element('p', 'crawler-menu-save-slot');
  const saveDetails = element('p', 'crawler-menu-save-details');
  const saveMessage = element('p', 'crawler-menu-save-message');
  const saveActions = element('div', 'crawler-menu-save-actions crawler-menu-actions');
  const menuSave = button('Save expedition', 'crawler-menu-save');
  const load = button('Load saved expedition', 'crawler-menu-load');
  const closeSaveLoad = button('Back to expedition', 'crawler-menu-close-save-load');
  const saveReturnTitle = button('Return to title', 'crawler-menu-save-return-title');
  const confirmation = element('div', 'crawler-menu-confirm');
  const confirmMessage = element('p', 'crawler-menu-confirm-message');
  const confirmActions = element('div', 'crawler-menu-confirm-actions crawler-menu-actions');
  const confirm = button('Return to title', 'crawler-menu-confirm-return');
  const cancel = button('Stay in the expedition', 'crawler-menu-cancel-return');
  const overwrite = element('div', 'crawler-menu-overwrite');
  const overwriteMessage = element('p', 'crawler-menu-overwrite-message');
  const overwriteActions = element('div', 'crawler-menu-overwrite-actions crawler-menu-actions');
  const confirmOverwrite = button('Overwrite saved expedition', 'crawler-menu-confirm-overwrite');
  const cancelOverwrite = button('Keep saved expedition', 'crawler-menu-cancel-overwrite');
  const loadConfirmation = element('div', 'crawler-menu-load-confirm');
  const loadConfirmMessage = element('p', 'crawler-menu-load-confirm-message');
  const loadConfirmActions = element('div', 'crawler-menu-load-confirm-actions crawler-menu-actions');
  const confirmLoad = button('Discard changes and load', 'crawler-menu-confirm-load');
  const cancelLoad = button('Keep current expedition', 'crawler-menu-cancel-load');
  // The Engine's downstream UI lane is transparent by default. Mark the deliberate fullscreen menu surface so
  // its backdrop and native controls participate in the same pointer arbitration as the buttons themselves.
  overlay.dataset.rustyUiInteractive = '';

  launch.addEventListener('click', () => host.claim(ACTIONS.returnTitle));
  newGame.addEventListener('click', () => host.claim(ACTIONS.newGame));
  continueGame.addEventListener('click', () => host.claim(ACTIONS.continue));
  menuSave.addEventListener('click', () => host.claim(ACTIONS.menuSave));
  load.addEventListener('click', () => host.claim(ACTIONS.load));
  closeSaveLoad.addEventListener('click', () => host.claim(ACTIONS.closeSaveLoad));
  saveReturnTitle.addEventListener('click', () => host.claim(ACTIONS.returnTitle));
  confirm.addEventListener('click', () => host.claim(ACTIONS.confirmReturnTitle));
  cancel.addEventListener('click', () => host.claim(ACTIONS.cancelReturnTitle));
  confirmOverwrite.addEventListener('click', () => host.claim(ACTIONS.menuSave));
  cancelOverwrite.addEventListener('click', () => host.claim(ACTIONS.cancelOverwrite));
  confirmLoad.addEventListener('click', () => host.claim(ACTIONS.confirmLoad));
  cancelLoad.addEventListener('click', () => host.claim(ACTIONS.cancelLoad));

  titleActions.append(newGame, continueGame);
  saveActions.append(menuSave, load, closeSaveLoad, saveReturnTitle);
  saveLoad.append(saveSlot, saveSummary, saveDetails, saveMessage, saveActions);
  confirmation.append(confirmMessage, confirmActions);
  confirmActions.append(confirm, cancel);
  overwrite.append(overwriteMessage, overwriteActions);
  overwriteActions.append(confirmOverwrite, cancelOverwrite);
  loadConfirmation.append(loadConfirmMessage, loadConfirmActions);
  loadConfirmActions.append(confirmLoad, cancelLoad);
  card.append(heading, subtitle, state, message, titleActions, saveLoad, confirmation, overwrite, loadConfirmation);
  overlay.append(card);
  root.append(launch, overlay);

  const render = (view: MenuView, title: string, ruleset: string): void => {
    root.dataset.menu = view.screen;
    root.dataset.state = view.state;
    root.dataset.code = view.code;
    launch.hidden = view.screen !== 'adventure' || !view.canReturnTitle;
    overlay.hidden = !view.visible || view.screen === 'adventure';
    heading.textContent = title;
    subtitle.textContent = ruleset;
    state.hidden = view.state === 'none';
    state.textContent = view.state === 'failed' ? 'The expedition could not be completed.' : view.state;
    message.hidden = view.message === '' || view.screen === 'confirm-return';
    message.textContent = view.message;
    titleActions.hidden = view.screen !== 'title';
    newGame.hidden = !view.canNewGame;
    continueGame.hidden = !view.canContinue;
    saveLoad.hidden = view.screen !== 'save-load';
    saveSlot.textContent = view.save.present ? `Slot: ${view.save.slot}` : `Slot: ${view.save.slot} (empty)`;
    saveSummary.replaceChildren(
      summary('Party', view.save.party || 'No saved party'),
      summary('Members', view.save.members > 0 ? String(view.save.members) : '—'),
      summary('Coins', String(view.save.coins)),
      summary('Provisions', String(view.save.provisions)),
      summary('Place', view.save.place || '—'),
      summary('Calendar', view.save.calendar || '—'),
      summary('Saved', view.save.savedAt || '—'),
    );
    saveDetails.textContent = view.save.present
      ? 'The saved expedition will replace this session after the load decision.'
      : view.save.available ? 'Save an expedition here to make Continue available from the title.' : 'Saving is unavailable in this session.';
    saveMessage.hidden = view.save.message === '';
    saveMessage.textContent = view.save.message;
    menuSave.disabled = !view.save.available;
    load.disabled = !view.save.available;
    load.toggleAttribute('disabled', !view.save.present || !view.save.available);
    confirmation.hidden = view.screen !== 'confirm-return';
    confirmMessage.textContent = view.message;
    confirmMessage.hidden = view.message === '';
    overwrite.hidden = view.screen !== 'confirm-overwrite';
    overwriteMessage.textContent = view.message;
    loadConfirmation.hidden = view.screen !== 'confirm-load';
    loadConfirmMessage.textContent = view.message;
  };

  return {
    element: root,
    render,
    clear() {
      root.dataset.menu = 'none';
      root.dataset.state = 'none';
      root.dataset.code = '';
      launch.hidden = true;
      overlay.hidden = true;
    },
  };
}

function summary(label: string, value: string): HTMLElement {
  const term = element('span', 'crawler-menu-save-label');
  term.textContent = label;
  const detail = element('span', 'crawler-menu-save-value');
  detail.textContent = value;
  const row = element('span');
  row.append(term, detail);
  return row;
}
