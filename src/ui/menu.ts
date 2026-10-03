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
  readonly screen: 'adventure' | 'title' | 'confirm-return';
  readonly canNewGame: boolean;
  readonly canContinue: boolean;
  readonly canReturnTitle: boolean;
  readonly hasUnsaved: boolean;
  readonly state: string;
  readonly code: string;
  readonly message: string;
}

/** Reads the menu block exactly as the Host/session projection published it. */
export function readMenu(f: Fields): MenuView {
  const screen = f.text('screen', 'adventure');
  return {
    visible: f.flag('visible'),
    screen: screen === 'title' || screen === 'confirm-return' ? screen : 'adventure',
    canNewGame: f.flag('canNewGame'),
    canContinue: f.flag('canContinue'),
    canReturnTitle: f.flag('canReturnTitle'),
    hasUnsaved: f.flag('hasUnsaved'),
    state: f.text('state', 'none'),
    code: f.text('code'),
    message: f.text('message'),
  };
}

/** The mounted menu and its projection-driven render operation. */
export interface Menu {
  readonly element: HTMLElement;
  render(view: MenuView, title: string, subtitle: string): void;
  clear(): void;
}

/** Mounts the title, continuation, and leave-unsaved controls. */
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
  const confirmation = element('div', 'crawler-menu-confirm');
  const confirmMessage = element('p', 'crawler-menu-confirm-message');
  const confirmActions = element('div', 'crawler-menu-confirm-actions crawler-menu-actions');
  const confirm = button('Return to title', 'crawler-menu-confirm-return');
  const cancel = button('Stay in the expedition', 'crawler-menu-cancel-return');

  launch.addEventListener('click', () => host.claim(ACTIONS.returnTitle));
  newGame.addEventListener('click', () => host.claim(ACTIONS.newGame));
  continueGame.addEventListener('click', () => host.claim(ACTIONS.continue));
  confirm.addEventListener('click', () => host.claim(ACTIONS.confirmReturnTitle));
  cancel.addEventListener('click', () => host.claim(ACTIONS.cancelReturnTitle));

  titleActions.append(newGame, continueGame);
  confirmation.append(confirmMessage, confirmActions);
  confirmActions.append(confirm, cancel);
  card.append(heading, subtitle, state, message, titleActions, confirmation);
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
    state.textContent = view.state === 'failed' ? 'The expedition could not be continued.' : view.state;
    message.hidden = view.message === '' || view.screen === 'confirm-return';
    message.textContent = view.message;
    titleActions.hidden = view.screen !== 'title';
    newGame.hidden = !view.canNewGame;
    continueGame.hidden = !view.canContinue;
    confirmation.hidden = view.screen !== 'confirm-return';
    confirmMessage.textContent = view.message;
    confirmMessage.hidden = view.message === '';
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
