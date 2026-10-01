/**
 * The names this companion shares with the product: the projection contract it reads, the intent and payload
 * contract it claims actions on, and the actions a row of a screen asks for.
 *
 * Every name here is the product's wire vocabulary, and the companion suite holds this list against the names
 * the product itself declares (`tests/PartyRpg.Ui.Tests/fixtures/contract.json`, written by the host suite), so a
 * name that drifts on either side fails a test rather than becoming a button nothing answers. The stand-alone
 * controls — pause, save, use, the fight's four, the five stops, the two ways out, and creation's two — are not
 * here: the projection publishes the action each of them sends, and the panel claims exactly that.
 */

/** The projection contract this companion renders; any other contract is not ours to interpret. */
export const UI_CONTRACT = 'crawler.ui.snapshot.v1';

/** The intent the product declares for semantic actions. */
export const UI_ACTION_INTENT = 'crawler.ui';

/** The payload contract those actions carry. */
export const UI_ACTION_CONTRACT = 'crawler.ui.action.v1';

/**
 * The actions a row of a screen asks for. Each names a choice, the product validates it, and its refusal is what
 * the screen shows.
 */
export const ACTIONS = {
  // The two actions the pause control may publish: which one a press sends is the product's answer, and the panel
  // names the button after it.
  pause: 'session.pause',
  resume: 'session.resume',
  // Creation: the choices a member is made with. The flow validates each, and its refusal is what the screen shows.
  selectMember: 'creation.select-member',
  selectPortrait: 'creation.select-portrait',
  selectClass: 'creation.select-class',
  setName: 'creation.set-name',
  raiseAttribute: 'creation.raise-attribute',
  lowerAttribute: 'creation.lower-attribute',
  chooseSkill: 'creation.choose-skill',
  removeSkill: 'creation.remove-skill',
  // A counter's commands. Entering a service is not one of them: the party enters by using the person it faces.
  serviceBuy: 'service.buy',
  serviceSell: 'service.sell',
  serviceIdentify: 'service.identify',
  serviceRepair: 'service.repair',
  serviceTeach: 'service.teach',
  serviceTrain: 'service.train',
  serviceFare: 'service.fare',
  // A theft names the line reached for and the member whose hand it is; a repayment names the account and the coins.
  serviceSteal: 'service.steal',
  serviceRepay: 'service.repay',
  // A conversation's choices: a topic to bring up, or another person to turn to.
  conversationTopic: 'conversation.topic',
  conversationPerson: 'conversation.person',
  // A hand in the purse of the person spoken with, naming the member who tries.
  conversationSteal: 'conversation.steal',
  // The skill-spend control: the member drawn and the skill on that row; the product judges ceiling and price.
  raiseSkill: 'party.raise-skill',
  // A casting names the member, the spell, and the target the product published for it — one action rather than a
  // mode, because a spell and what it is aimed at are one decision. The quick slot names the spell it should hold.
  cast: 'party.cast',
  quickSpell: 'party.quick-spell',
  // A mixture names two of the things the party carries and the member who puts them together.
  mix: 'party.mix',
  // A change of equipment names the member and the thing put on, or the member and the slot emptied.
  equip: 'party.equip',
  unequip: 'party.unequip',
} as const;

/** Asks the product for one action on its payload contract, with whatever the action carries. */
export type Claim = (action: string, data?: Readonly<Record<string, string | number>>) => void;
