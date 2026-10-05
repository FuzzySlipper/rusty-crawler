import { button, element } from './dom.js';
import type { Fields } from './reader.js';

/** The product's earned ending. Dismissal is only local presentation state. */
export interface CompletionView {
  readonly completed: boolean;
  readonly id: string;
  readonly title: string;
  readonly text: string;
  readonly continues: boolean;
}

export function readCompletion(f: Fields): CompletionView {
  return { completed: f.flag('completed'), id: f.text('id'), title: f.text('title'), text: f.text('text'), continues: f.flag('continues') };
}

export function mountCompletion(focusGameplay: () => void) {
  const surface = element('section', 'crawler-ending');
  surface.setAttribute('aria-label', 'Expedition complete');
  const card = element('div', 'crawler-ending-card');
  const banner = element('p', 'crawler-ending-banner');
  banner.textContent = 'Expedition complete';
  const title = element('h1');
  const words = element('p');
  const onward = button('Continue exploring');
  const recall = button('Read the expedition’s ending');
  surface.hidden = recall.hidden = true;
  let seen = '';
  let dismissed = false;
  onward.addEventListener('click', () => { dismissed = true; surface.hidden = true; focusGameplay(); });
  recall.addEventListener('click', () => { dismissed = false; surface.hidden = false; });
  card.append(banner, title, words, onward);
  surface.append(card);
  return {
    element: surface,
    recall,
    clear() { surface.hidden = recall.hidden = true; seen = ''; dismissed = false; },
    render(view: CompletionView, menuVisible: boolean) {
      if (view.id !== seen) { seen = view.id; dismissed = false; }
      title.textContent = view.title;
      words.textContent = view.text;
      onward.hidden = !view.continues;
      recall.hidden = !view.completed;
      surface.hidden = !view.completed || dismissed || menuVisible;
    },
  };
}
