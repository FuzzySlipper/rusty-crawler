/**
 * The counter the party stands at: what its shelves hold and at what price, what it teaches, what it would buy,
 * identify, and mend, the passages it sells, and what the last command did.
 *
 * Every list is rebuilt from the projection, so the screen holds nothing the product did not publish and decides no
 * price and no eligibility itself: which rows a command applies to — a lot still on the shelf, an item still to be
 * identified, one that is damaged — is the product's own list.
 */

import type { ControlView } from './overview.js';
import type { Fields } from './reader.js';
import { ACTIONS } from './actions.js';
import { button, element, options, redrawGuard, section, type Host, type Section } from './dom.js';

/** One line of a service's shelves. */
export interface ServiceStockView {
  /** The lot's identity, which a buy command names. */
  readonly lot: string;
  readonly item: string;
  readonly name: string;
  /** How many are left on the shelves. */
  readonly count: number;
  /** What one costs the party. */
  readonly price: number;
  /** Whether the counter is reselling something it bought from the party. */
  readonly sale: boolean;
  /** The URL the Engine serves the item's picture at, empty when it has none. */
  readonly image: string;
  /** Whether the counter would sell one now. */
  readonly canBuy: boolean;
  /** Whether a thief could reach for one now. */
  readonly canSteal: boolean;
}

/** One lesson a service teaches. */
export interface ServiceLessonView {
  /** `skill`, `spell`, or `membership`. */
  readonly kind: string;
  /** The skill's or effect's id, which a teach command names. */
  readonly subject: string;
  readonly name: string;
  readonly amount: number;
  readonly price: number;
  /** The rung of a skill's ladder the lesson leaves a member at, where one is the first rung. */
  readonly tier: number;
}

/** One of the party's own items, as a counter that would buy it shows it. */
export interface ServiceSaleView {
  /** The instance's durable identity, which a sell command names. */
  readonly item: string;
  readonly definition: string;
  readonly name: string;
  /** What the counter would pay the party for it. */
  readonly price: number;
  readonly damage: number;
  readonly identified: boolean;
  /** Whether the item carries the stolen mark, which a counter may refuse to deal in. */
  readonly stolen: boolean;
  readonly image: string;
}

/** One of the party's items a counter would identify or repair. */
export interface ServiceHeldView {
  /** The instance's durable identity, which the command names. */
  readonly item: string;
  readonly name: string;
  /** The URL the Engine serves the item's picture at, empty when it has none. */
  readonly image: string;
  /** What the counter would charge for the work, quoted before anything is settled. */
  readonly price: number;
}

/** The command each offer choice's operation names, which every counter surface sends. */
export const OFFER_ACTIONS: Readonly<Record<string, string>> = {
  cure: ACTIONS.serviceCure,
  train: ACTIONS.serviceTrain,
  provision: ACTIONS.serviceProvision,
  stay: ACTIONS.serviceStay,
  deposit: ACTIONS.serviceDeposit,
  withdraw: ACTIONS.serviceWithdraw,
  fare: ACTIONS.serviceFare,
};

/** One thing a counter offers besides goods and lessons: a passage, a cure, a room, a line it posts. */
export interface ServiceOfferView {
  readonly kind: string;
  readonly subject: string;
  readonly name: string;
  readonly amount: number;
  readonly price: number;
  readonly choices: readonly ServiceOfferChoiceView[];
}

/** A command the mechanism priced for its actual patient or amount. */
export interface ServiceOfferChoiceView {
  readonly operation: string;
  readonly member: number;
  readonly name: string;
  readonly count: number;
  readonly price: number;
  readonly payment: number;
  readonly enabled: boolean;
  readonly reason: string;
}

/** One passage a counter sells, which a fare command names by the place it reaches. */
export interface ServiceFareView {
  readonly subject: string;
  readonly name: string;
  readonly price: number;
}

/** One debt the party owes on an account the counter collects. */
export interface ServiceDebtView {
  /** The account, which a repayment names. */
  readonly subject: string;
  readonly name: string;
  /** What is owed on it. */
  readonly owed: number;
  /** What a repayment of all of it would take from the purse now. */
  readonly price: number;
  /** Whether a repayment would take anything now. */
  readonly canRepay: boolean;
}

/** One member a lesson could be taught to. */
export interface ServiceMemberView {
  readonly index: number;
  readonly name: string;
}

/**
 * The counter the party is standing at. `available` is false when the session holds no service mechanism at all;
 * `open` says whether a visit is; `state` says whether the counter is serving.
 */
export interface ServiceView {
  readonly amount: number;
  readonly available: boolean;
  readonly open: boolean;
  readonly id: string;
  readonly kind: string;
  readonly name: string;
  readonly proprietor: string;
  /** `open`, `closed`, or empty when the party stands at no counter. */
  readonly state: string;
  readonly hours: string;
  readonly operations: readonly string[];
  readonly memberships: readonly string[];
  readonly stock: readonly ServiceStockView[];
  readonly lessons: readonly ServiceLessonView[];
  readonly offers: readonly ServiceOfferView[];
  readonly sales: readonly ServiceSaleView[];
  readonly members: readonly ServiceMemberView[];
  readonly identify: readonly ServiceHeldView[];
  readonly repair: readonly ServiceHeldView[];
  readonly fares: readonly ServiceFareView[];
  readonly debts: readonly ServiceDebtView[];
  /** The members the product says could try the shelves without paying. */
  readonly thieves: readonly ServiceMemberView[];
  /** Whether the counter keeps a shelf a thief could reach, with somebody able to try. */
  readonly canSteal: boolean;
  /** Whether the counter sells from its shelves at all. */
  readonly canBuy: boolean;
  /** Whether it buys the party's items. */
  readonly canSell: boolean;
  /** Whether it teaches. */
  readonly canTeach: boolean;
  /** What the last command asked for, empty before any. */
  readonly action: string;
  /** `none`, `applied`, or `refused`. */
  readonly outcome: string;
  readonly code: string;
  readonly message: string;
  readonly paid: number;
  readonly earned: number;
  /** What the party's one purse holds. */
  readonly coins: number;
}

function readHeld(entry: Fields): ServiceHeldView {
  return { item: entry.text('item'), name: entry.text('name'), image: entry.text('image'), price: entry.number('price') };
}

export function readService(f: Fields): ServiceView {
  return {
    amount: f.number('amount'),
    available: f.flag('available'),
    open: f.flag('open'),
    id: f.text('id'),
    kind: f.text('kind'),
    name: f.text('name'),
    proprietor: f.text('proprietor'),
    state: f.text('state'),
    hours: f.text('hours'),
    operations: f.words('operations'),
    memberships: f.words('memberships'),
    stock: f.list('stock', (entry) => ({
      lot: entry.text('lot'),
      item: entry.text('item'),
      name: entry.text('name'),
      count: entry.number('count'),
      price: entry.number('price'),
      sale: entry.flag('sale'),
      image: entry.text('image'),
      canBuy: entry.flag('canBuy'),
      canSteal: entry.flag('canSteal'),
    })),
    lessons: f.list('lessons', (entry) => ({
      kind: entry.text('kind'),
      subject: entry.text('subject'),
      name: entry.text('name'),
      amount: entry.number('amount'),
      price: entry.number('price'),
      tier: entry.number('tier'),
    })),
    offers: f.list('offers', (entry) => ({
      kind: entry.text('kind'),
      subject: entry.text('subject'),
      name: entry.text('name'),
      amount: entry.number('amount'),
      price: entry.number('price'),
      choices: entry.list('choices', (choice) => ({
        operation: choice.text('operation'), member: choice.number('member'), name: choice.text('name'),
        count: choice.number('count'), price: choice.number('price'), payment: choice.number('payment'),
        enabled: choice.flag('enabled'), reason: choice.text('reason'),
      })),
    })),
    sales: f.list('sales', (entry) => ({
      item: entry.text('item'),
      definition: entry.text('definition'),
      name: entry.text('name'),
      price: entry.number('price'),
      damage: entry.number('damage'),
      identified: entry.flag('identified'),
      stolen: entry.flag('stolen'),
      image: entry.text('image'),
    })),
    members: f.list('members', (entry) => ({ index: entry.number('index'), name: entry.text('name') })),
    identify: f.list('identify', readHeld),
    repair: f.list('repair', readHeld),
    fares: f.list('fares', (entry) => ({ subject: entry.text('subject'), name: entry.text('name'), price: entry.number('price') })),
    debts: f.list('debts', (entry) => ({
      subject: entry.text('subject'),
      name: entry.text('name'),
      owed: entry.number('owed'),
      price: entry.number('price'),
      canRepay: entry.flag('canRepay'),
    })),
    thieves: f.list('thieves', (entry) => ({ index: entry.number('index'), name: entry.text('name') })),
    canSteal: f.flag('canSteal'),
    canBuy: f.flag('canBuy'),
    canSell: f.flag('canSell'),
    canTeach: f.flag('canTeach'),
    action: f.text('action'),
    outcome: f.text('outcome', 'none'),
    code: f.text('code'),
    message: f.text('message'),
    paid: f.number('paid'),
    earned: f.number('earned'),
    coins: f.number('coins'),
  };
}

/** What the counter's screen draws from: the counter, and the control that leaves it. */
export interface ServiceReading {
  readonly service: ServiceView;
  readonly leave: ControlView;
}

/** Mounts the counter's screen. */
export function mountService(host: Host): Section<ServiceReading> {
  const { panel, claim } = host;
  const service = section('crawler-service');
  const serviceHead = element('p', 'crawler-step-head');
  const state = element('p', 'crawler-service-state');
  const access = element('p', 'crawler-service-access');
  const memberRow = element('div', 'crawler-service-member');
  const memberLabel = element('span', 'crawler-row-label');
  memberLabel.textContent = 'Lesson goes to';
  const memberSelect = element('select');
  memberRow.append(memberLabel, memberSelect);
  const stockRow = element('div', 'crawler-row');
  const stealRow = element('div', 'crawler-row');
  const debtRow = element('div', 'crawler-row');
  const saleRow = element('div', 'crawler-row');
  const lessonRow = element('div', 'crawler-row');
  const offerRow = element('div', 'crawler-row');
  const amountRow = element('div', 'crawler-service-amount');
  const amountLabel = element('label');
  amountLabel.textContent = 'Coins to move ';
  const amountInput = element('input');
  amountInput.type = 'number';
  amountInput.step = '1';
  amountInput.min = '1';
  amountLabel.append(amountInput);
  const quoteAmount = button('Show prices for this amount');
  quoteAmount.addEventListener('click', () => claim(ACTIONS.serviceAmount, { count: Number(amountInput.value) }));
  amountRow.append(amountLabel, quoteAmount);
  const outcome = element('p', 'crawler-service-result');
  outcome.hidden = true;
  const actions = element('div', 'crawler-actions');
  const leave = button('Leave the counter');
  leave.addEventListener('click', () => {
    if (leave.dataset.action !== undefined && leave.dataset.action !== '') claim(leave.dataset.action);
  });
  actions.append(leave);
  service.append(serviceHead, state, access, memberRow, stockRow, stealRow, amountRow, offerRow, debtRow, saleRow, lessonRow, actions, outcome);
  const changed = redrawGuard();

  const render = ({ service: view, leave: control }: ServiceReading): void => {
    // A session with no mechanism, one that stands at no counter, one that is browsing, and one that was turned
    // away are four different facts: a panel that called the second 'closed' would show every player walking the
    // street as standing at a shut shop.
    const shown = view.available && (view.open || view.name !== '' || view.outcome !== 'none');
    panel.dataset.service = !view.available ? 'none' : view.open ? 'open' : shown ? 'closed' : 'away';
    panel.dataset.serviceState = view.state === '' ? 'unknown' : view.state;
    panel.dataset.serviceAction = view.action;
    panel.dataset.serviceOutcome = view.outcome;
    // A counter is shown while a visit is open, and also when the party was turned away from one.
    service.hidden = !shown;
    service.dataset.operations = view.operations.join(' ');
    serviceHead.textContent = view.name === '' ? '' : `${view.name}${view.proprietor === '' ? '' : ` · ${view.proprietor}`}`;
    state.textContent =
      view.state === ''
        ? view.kind
        : `${view.kind}${view.kind === '' ? '' : ' · '}${view.state}${view.hours === '' ? '' : ` (${view.hours})`}`;
    access.textContent = view.memberships.length === 0 ? '' : `Membership: ${view.memberships.join(', ')}`;
    outcome.hidden = view.message === '';
    outcome.dataset.outcome = view.outcome;
    outcome.dataset.code = view.code;
    outcome.textContent =
      view.message === ''
        ? ''
        : view.paid > 0
          ? `${view.message} Paid ${view.paid}; the purse holds ${view.coins}.`
          : view.earned > 0
            ? `${view.message} Received ${view.earned}; the purse holds ${view.coins}.`
            : view.message;
    leave.disabled = !control.enabled;
    leave.dataset.action = control.action;
    if (!changed(view)) return;

    if (!view.open) {
      for (const row of [stockRow, stealRow, offerRow, debtRow, saleRow, lessonRow]) row.replaceChildren();
      memberRow.hidden = true;
      amountRow.hidden = true;
      return;
    }

    // The picker keeps the member it shows across a redraw, because a lesson goes to whoever the player chose.
    const member = memberSelect.value;
    memberSelect.replaceChildren(
      ...view.members.map((entry) => {
        const option = element('option');
        option.value = String(entry.index);
        option.textContent = entry.name;
        return option;
      }),
    );
    if (member !== '') memberSelect.value = member;
    memberRow.hidden = view.members.length < 2;

    stockRow.replaceChildren(
      view.stock.length === 0 && !view.canBuy
        ? element('div')
        : options(
            claim,
            'For sale',
            view.stock.map((entry) => ({
              id: entry.lot,
              text: entry.count === 0 ? `${entry.name} — sold out` : `${entry.name} ×${entry.count} — ${entry.price}${entry.sale ? ' (yours)' : ''}`,
              action: view.canBuy ? ACTIONS.serviceBuy : undefined,
              enabled: entry.canBuy,
              payload: () => ({ target: entry.lot, count: 1 }),
            })),
          ),
    );

    // What a thief could reach for: one row per line still on the shelf and member the product named, which sends
    // that member's hand for that line. The product draws whether anybody sees it.
    stealRow.replaceChildren(
      !view.canSteal
        ? element('div')
        : options(
            claim,
            'Steal',
            view.thieves.flatMap((thief) =>
              view.stock.map((entry) => ({
                id: `steal-${thief.index}-${entry.lot}`,
                text: `${thief.name}: ${entry.name}`,
                action: ACTIONS.serviceSteal,
                enabled: entry.canSteal,
                payload: () => ({ target: entry.lot, member: thief.index }),
              })),
            ),
          ),
    );

    // What the party owes on an account this counter collects: a row that pays toward it what the product says
    // the purse would hand over now.
    debtRow.replaceChildren(
      view.debts.length === 0
        ? element('div')
        : options(
            claim,
            'Owed',
            view.debts.map((entry) => ({
              id: `repay-${entry.subject}`,
              text: `${entry.name}: ${entry.owed} owed — pay ${entry.price}`,
              action: ACTIONS.serviceRepay,
              enabled: entry.canRepay,
              payload: () => ({ target: entry.subject, count: entry.price }),
            })),
          ),
    );

    // What the party carries that the counter would buy, identify, or mend: the product's own lists, one row each.
    const held = [
      ...(view.canSell ? view.sales : []).map((entry) => ({
        id: `sell-${entry.item}`,
        text: `Sell ${entry.name}${entry.damage > 0 ? `, damaged ${entry.damage}` : ''}${entry.identified ? '' : ', unidentified'}${entry.stolen ? ', stolen' : ''} — ${entry.price}`,
        action: ACTIONS.serviceSell,
        payload: () => ({ target: entry.item }),
      })),
      ...view.identify.map((entry) => ({
        id: `identify-${entry.item}`,
        text: `Identify ${entry.name}`,
        action: ACTIONS.serviceIdentify,
        payload: () => ({ target: entry.item }),
      })),
      ...view.repair.map((entry) => ({
        id: `repair-${entry.item}`,
        text: `Repair ${entry.name}`,
        action: ACTIONS.serviceRepair,
        payload: () => ({ target: entry.item }),
      })),
    ];
    saleRow.replaceChildren(held.length === 0 ? element('div') : options(claim, 'Your items', held));

    amountRow.hidden = !view.offers.some((offer) => offer.kind === 'holding' && offer.choices.length > 0);
    amountInput.value = String(view.amount);
    offerRow.replaceChildren(...view.offers.filter((offer) => offer.kind !== 'debt').map((offer) => options(
      claim,
      offer.kind === 'holding' ? `${offer.name}: ${offer.amount} held; quoting ${view.amount}` : offer.name,
      offer.choices.map((choice) => ({
        id: `${choice.operation}-${offer.subject}-${choice.member}`,
        text: `${choice.operation}${choice.name === '' ? '' : ` ${choice.name}`} — pay ${choice.price}${choice.payment === 0 ? '' : `, receive ${choice.payment}`}${choice.reason === '' ? '' : ` · ${choice.reason}`}`,
        action: OFFER_ACTIONS[choice.operation],
        enabled: choice.enabled,
        payload: () => ({ target: offer.subject, member: choice.member, count: choice.count }),
      })),
    )));

    lessonRow.replaceChildren(
      view.lessons.length === 0
        ? element('div')
        : options(
            claim,
            'Taught here',
            view.lessons.map((entry) => ({
              // A skill can be taught at more than one rung, so the row's identity is the subject and the rung
              // together: two lessons of one skill are two rows, and the one a player presses is the one it names.
              id: `${entry.subject}@${entry.tier}`,
              // A lesson that grants a skill says the level it leaves it at; one that grants a rung of a skill says
              // the rung in its own name, which the product composed.
              text: entry.kind === 'skill' && entry.tier <= 1 ? `${entry.name} to level ${entry.amount} — ${entry.price}` : `${entry.name} — ${entry.price}`,
              action: view.canTeach ? ACTIONS.serviceTeach : undefined,
              payload: () => ({
                target: entry.subject,
                tier: entry.tier,
                member: Number(memberSelect.value === '' ? '0' : memberSelect.value),
              }),
            })),
          ),
    );
  };

  return { element: service, render };
}
