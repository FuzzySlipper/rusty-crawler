/**
 * The counter screen: the establishment the party stands at, its keeper, its hours and the party's purse, and a page
 * for each thing it does — wares with their pictures, what it would buy, identify or mend, its lessons and
 * memberships, healing, training, food, a room, the bank, passages, its notices and what the party owes — each laid
 * out for what it is rather than as one list of commands.
 *
 * Every row, price and refusal is the service mechanism's own, published before anything is settled; a control is
 * offered when the product says it would be taken. Which page is open, the lot or item picked and the member a lesson
 * goes to are presentation state.
 */

import type { ControlView } from './overview.js';
import { ACTIONS } from './actions.js';
import { button, element, heldPicker, redrawGuard, report, result, section, type Host, type Section } from './dom.js';
import { OFFER_ACTIONS, type ServiceOfferView, type ServiceView } from './service.js';

/** What the counter screen draws from: the counter, and the control that leaves it. */
export interface CounterReading {
  readonly service: ServiceView;
  readonly leave: ControlView;
}

/** The pages a counter may have, in the order they are tabbed, and the word each is called by. */
const PAGES = [
  ['wares', 'Wares'],
  ['sell', 'Sell'],
  ['identify', 'Identify'],
  ['repair', 'Repair'],
  ['learn', 'Learn'],
  ['cure', 'Healing'],
  ['training', 'Training'],
  ['provision', 'Food'],
  ['stay', 'Room'],
  ['holding', 'Bank'],
  ['fare', 'Travel'],
  ['notice', 'Notices'],
  ['debt', 'Debts'],
] as const;
type Page = (typeof PAGES)[number][0];


/** A picture tile with a caption, or the caption alone where there is no picture. */
function tile(name: string, image: string, caption: string): HTMLButtonElement {
  const made = button('', 'crawler-counter-tile');
  made.title = name;
  if (image !== '') {
    const picture = element('img', 'crawler-item-picture');
    picture.src = image;
    picture.alt = name;
    made.append(picture);
  } else {
    const words = element('span', 'crawler-item-words');
    words.textContent = name;
    made.append(words);
  }

  const under = element('span', 'crawler-counter-caption');
  under.textContent = caption;
  made.append(under);
  return made;
}

/** Mounts the counter screen. */
export function mountCounter(host: Host): Section<CounterReading> {
  const { claim } = host;
  const counter = section('crawler-counter');
  const head = element('div', 'crawler-counter-head');
  const title = element('p', 'crawler-counter-title');
  const state = element('p', 'crawler-counter-state');
  const purse = element('p', 'crawler-counter-purse');
  head.append(title, state, purse);
  const outcome = result('crawler-counter-outcome');
  const tabs = element('div', 'crawler-character-tabs');
  const body = element('div', 'crawler-counter-page');
  const actions = element('div', 'crawler-actions');
  const leave = button('Leave the counter', 'crawler-counter-leave');
  leave.addEventListener('click', () => {
    if (leave.dataset.action !== undefined && leave.dataset.action !== '') claim(leave.dataset.action);
  });
  actions.append(leave);
  counter.append(head, outcome, tabs, body, actions);
  const changed = redrawGuard();
  let page: Page | '' = '';
  let picked = '';
  const learners = new Map<string, string>();
  let last: CounterReading | null = null;
  const redraw = (): void => {
    if (last !== null) render(last);
  };

  /** The pages this counter has: a page for each thing the product lists it doing. */
  const pagesOf = (view: ServiceView): Page[] =>
    PAGES.map(([id]) => id).filter((id) => {
      if (id === 'wares') return view.stock.length > 0 || view.canBuy;
      if (id === 'sell') return view.canSell;
      if (id === 'identify') return view.identify.length > 0;
      if (id === 'repair') return view.repair.length > 0;
      if (id === 'learn') return view.lessons.length > 0;
      if (id === 'debt') return view.debts.length > 0;
      return view.offers.some((offer) => offer.kind === id);
    });

  /** The wares: a tile per lot, and the picked lot with its price, Buy, and each thief's hand. */
  const wares = (view: ServiceView): HTMLElement[] => {
    const grid = element('div', 'crawler-counter-grid');
    for (const lot of view.stock) {
      const piece = tile(lot.name, lot.image, lot.count === 0 ? 'sold out' : `${lot.price} gold`);
      piece.dataset.lot = lot.lot;
      piece.dataset.picked = String(lot.lot === picked);
      piece.addEventListener('click', () => {
        picked = lot.lot;
        redraw();
      });
      grid.append(piece);
    }

    const chosen = view.stock.find((lot) => lot.lot === picked);
    const detail = element('div', 'crawler-counter-detail');
    if (chosen === undefined) {
      detail.append(hint(view.stock.length === 0 ? 'The shelves are bare.' : 'Pick something on the shelves.'));
    } else {
      const name = element('p', 'crawler-inspect-name');
      name.textContent = chosen.name;
      const line = element('p', 'crawler-inspect-kind');
      line.textContent = chosen.count === 0 ? 'Sold out' : `${chosen.count} left · ${chosen.price} gold${chosen.sale ? ' · bought from the party' : ''}`;
      const buy = button(`Buy for ${chosen.price}`, 'crawler-counter-buy');
      buy.disabled = !chosen.canBuy;
      buy.addEventListener('click', () => claim(ACTIONS.serviceBuy, { target: chosen.lot, count: 1 }));
      detail.append(name, line, buy);
      // A thief's hand is the counter's own operation: offered for a line the product says can be reached.
      if (view.canSteal) {
        for (const thief of view.thieves) {
          const steal = button(`${thief.name}: steal it`, 'crawler-counter-steal');
          steal.disabled = !chosen.canSteal;
          steal.addEventListener('click', () => claim(ACTIONS.serviceSteal, { target: chosen.lot, member: thief.index }));
          detail.append(steal);
        }
      }
    }

    return [grid, detail];
  };

  /** The party's own items a counter works on: what it would pay, identify or mend, each with its command. */
  const held = (
    rows: readonly { readonly item: string; readonly name: string; readonly image: string; readonly caption: string }[],
    verb: string,
    action: string,
    none: string,
  ): HTMLElement[] => {
    if (rows.length === 0) return [hint(none)];
    const grid = element('div', 'crawler-counter-grid');
    for (const row of rows) {
      const piece = tile(row.name, row.image, row.caption);
      piece.dataset.item = row.item;
      piece.dataset.picked = String(row.item === picked);
      piece.addEventListener('click', () => {
        picked = row.item;
        redraw();
      });
      grid.append(piece);
    }

    const chosen = rows.find((row) => row.item === picked);
    const detail = element('div', 'crawler-counter-detail');
    if (chosen === undefined) {
      detail.append(hint('Pick one of the party’s items.'));
    } else {
      const name = element('p', 'crawler-inspect-name');
      name.textContent = chosen.name;
      const command = button(`${verb} · ${chosen.caption}`, 'crawler-counter-work');
      command.addEventListener('click', () => claim(action, { target: chosen.item }));
      detail.append(name, command);
    }

    return [grid, detail];
  };

  /** The lessons and memberships, each taught to the member the player picks. */
  const learn = (view: ServiceView): HTMLElement[] => {
    const who = element('div', 'crawler-counter-who');
    const label = element('span', 'crawler-row-label');
    label.textContent = 'Taught to';
    const member = heldPicker('crawler-counter-learner', view.members.map((entry) => ({ value: String(entry.index), text: entry.name })), learners, view.id);
    who.append(label, member);
    const list = element('div', 'crawler-counter-cards');
    for (const lesson of view.lessons) {
      const card = element('div', 'crawler-counter-card');
      card.dataset.subject = lesson.subject;
      card.dataset.kind = lesson.kind;
      const name = element('p', 'crawler-inspect-name');
      name.textContent = lesson.name;
      const what = element('p', 'crawler-inspect-kind');
      what.textContent =
        lesson.kind === 'membership'
          ? `Membership · ${lesson.price} gold`
          : lesson.kind === 'spell'
            ? `Spell · ${lesson.price} gold`
            : lesson.tier <= 1
              ? `Skill to level ${lesson.amount} · ${lesson.price} gold`
              : `Skill · ${lesson.price} gold`;
      const teach = button('Learn', 'crawler-counter-teach');
      teach.disabled = !view.canTeach;
      teach.addEventListener('click', () =>
        claim(ACTIONS.serviceTeach, { target: lesson.subject, tier: lesson.tier, member: Number(member.value === '' ? '0' : member.value) }),
      );
      card.append(name, what, teach);
      list.append(card);
    }

    return [who, list];
  };

  /** One offer: its name and each choice the mechanism priced for its patient or amount, with why it would be refused. */
  const offerCard = (offer: ServiceOfferView): HTMLElement => {
    const card = element('div', 'crawler-counter-card');
    card.dataset.kind = offer.kind;
    card.dataset.subject = offer.subject;
    const name = element('p', 'crawler-inspect-name');
    name.textContent = offer.kind === 'holding' ? `${offer.name}: ${offer.amount} held` : offer.name;
    card.append(name);
    for (const choice of offer.choices) {
      const row = element('div', 'crawler-counter-choice');
      const control = button(
        `${choice.name === '' ? choice.operation : `${choice.operation} ${choice.name}`} — pay ${choice.price}${choice.payment === 0 ? '' : `, receive ${choice.payment}`}`,
        'crawler-counter-offer',
      );
      control.dataset.operation = choice.operation;
      control.dataset.member = String(choice.member);
      control.disabled = !choice.enabled;
      const action = OFFER_ACTIONS[choice.operation];
      if (action !== undefined) control.addEventListener('click', () => claim(action, { target: offer.subject, member: choice.member, count: choice.count }));
      row.append(control);
      if (choice.reason !== '') {
        const why = element('span', 'crawler-counter-reason');
        why.textContent = choice.reason;
        row.append(why);
      }

      card.append(row);
    }

    return card;
  };

  /** The bank's amount: the coins to move, quoted by the product before any choice is pressed. */
  const amount = (view: ServiceView): HTMLElement => {
    const row = element('div', 'crawler-counter-amount');
    const label = element('label');
    label.textContent = 'Coins to move ';
    const input = element('input');
    input.type = 'number';
    input.step = '1';
    input.min = '1';
    input.value = String(view.amount);
    label.append(input);
    const quote = button('Quote this amount', 'crawler-counter-quote');
    quote.addEventListener('click', () => claim(ACTIONS.serviceAmount, { count: Number(input.value) }));
    row.append(label, quote);
    return row;
  };

  /** What the party owes on accounts this counter collects. */
  const debts = (view: ServiceView): HTMLElement[] =>
    view.debts.map((debt) => {
      const card = element('div', 'crawler-counter-card');
      card.dataset.subject = debt.subject;
      const name = element('p', 'crawler-inspect-name');
      name.textContent = `${debt.name}: ${debt.owed} owed`;
      const repay = button(`Repay ${debt.price}`, 'crawler-counter-repay');
      repay.disabled = !debt.canRepay;
      repay.addEventListener('click', () => claim(ACTIONS.serviceRepay, { target: debt.subject, count: debt.price }));
      card.append(name, repay);
      return card;
    });

  const render = (reading: CounterReading): void => {
    last = reading;
    const { service: view, leave: control } = reading;
    counter.hidden = !view.available || !view.open;
    counter.dataset.service = view.id;
    title.textContent = view.name === '' ? '' : `${view.name}${view.proprietor === '' ? '' : ` · ${view.proprietor}`}`;
    state.textContent = `${view.kind}${view.state === '' ? '' : ` · ${view.state}`}${view.hours === '' ? '' : ` (${view.hours})`}`;
    state.dataset.state = view.state;
    purse.textContent = `${view.coins} gold`;
    report(outcome, view.outcome, view.code, view.message);
    leave.disabled = !control.enabled;
    leave.dataset.action = control.action;
    const pages = pagesOf(view);
    if (page === '' || !pages.includes(page)) page = pages.length > 0 ? (pages[0] as Page) : '';
    counter.dataset.page = page;
    if (!changed({ view, page, picked })) return;

    tabs.replaceChildren(
      ...PAGES.filter(([id]) => pages.includes(id)).map(([id, label]) => {
        const tab = button(label, 'crawler-character-tab');
        tab.dataset.page = id;
        tab.dataset.open = id === page ? 'yes' : 'no';
        tab.addEventListener('click', () => {
          page = id;
          picked = '';
          redraw();
        });
        return tab;
      }),
    );

    const shown: Page | '' = page;
    if (shown === '') body.replaceChildren(hint(view.state === 'closed' ? 'The counter is closed.' : 'This counter offers nothing now.'));
    else if (page === 'wares') body.replaceChildren(...wares(view));
    else if (page === 'sell')
      body.replaceChildren(
        ...held(
          view.sales.map((sale) => ({
            item: sale.item,
            name: sale.name,
            image: sale.image,
            caption: `${sale.price} gold${sale.damage > 0 ? ' · broken' : ''}${sale.stolen ? ' · stolen' : ''}`,
          })),
          'Sell',
          ACTIONS.serviceSell,
          'The party carries nothing this counter would buy.',
        ),
      );
    else if (page === 'identify')
      body.replaceChildren(...held(view.identify.map((row) => ({ ...row, caption: `${row.price} gold` })), 'Identify', ACTIONS.serviceIdentify, ''));
    else if (page === 'repair')
      body.replaceChildren(...held(view.repair.map((row) => ({ ...row, caption: `${row.price} gold` })), 'Repair', ACTIONS.serviceRepair, ''));
    else if (page === 'learn') body.replaceChildren(...learn(view));
    else if (page === 'debt') body.replaceChildren(...debts(view));
    else if (page === 'notice')
      body.replaceChildren(
        ...view.offers
          .filter((offer) => offer.kind === 'notice')
          .map((offer) => {
            const line = element('p', 'crawler-counter-notice');
            line.textContent = offer.name;
            return line;
          }),
      );
    else {
      const cards = view.offers.filter((offer) => offer.kind === page).map(offerCard);
      body.replaceChildren(...(page === 'holding' ? [amount(view), ...cards] : cards));
    }
  };

  return { element: counter, render };
}

/** A quiet line that says what a page holds when it holds nothing to press. */
function hint(text: string): HTMLElement {
  const made = element('p', 'crawler-inspect-hint');
  made.textContent = text;
  return made;
}
