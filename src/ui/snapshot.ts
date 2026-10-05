/**
 * Reads one projection value into every block the panel draws, and names every field that was missing or of
 * another type.
 *
 * A projection without a composition or a session is not one this companion understands, and rendering half of it
 * would show a panel that disagrees with the product, so it reads as nothing. Every other block is read on the
 * reader's own terms: as published when every field of it could be read, and as its no-mechanism reading — with the
 * problems named — when any could not.
 */

import { readAlchemy, type AlchemyView } from './alchemy.js';
import { readCharacter, type CharacterView } from './character.js';
import { readCombat, type CombatView } from './combat.js';
import { readCompletion, type CompletionView } from './completion.js';
import { readConversation, type ConversationView } from './conversation.js';
import { readCreation, type CreationView } from './creation.js';
import { readEquipment, type EquipmentView } from './equipment.js';
import { readFeedback, type FeedbackView } from './feedback.js';
import { readJournal, type JournalView } from './journal.js';
import { readMagic, type MagicView } from './magic.js';
import { readMap, type MapView } from './map.js';
import { readMenu, type MenuView } from './menu.js';
import {
  readClock,
  readComposition,
  readControls,
  readInteraction,
  readMovement,
  readParty,
  readSave,
  readSession,
  readWorld,
  type ClockView,
  type CompositionView,
  type ControlsView,
  type InteractionView,
  type MovementView,
  type PartyView,
  type SaveView,
  type SessionView,
  type WorldView,
} from './overview.js';
import { readProgression, type ProgressionView } from './progression.js';
import { readPromotion, type PromotionView } from './promotion.js';
import { readQuests, type QuestsView } from './quests.js';
import { block, fields, type Fields, type Problems } from './reader.js';
import { readRest, type RestView } from './rest.js';
import { readService, type ServiceView } from './service.js';
import { readSkills, type SkillsView } from './skills.js';

/** Every block of one projection, as the panel draws it. */
export interface SnapshotView {
  readonly completion: CompletionView;
  readonly composition: CompositionView;
  readonly menu: MenuView;
  readonly session: SessionView;
  readonly world: WorldView;
  readonly movement: MovementView;
  readonly clock: ClockView;
  readonly party: PartyView;
  readonly creation: CreationView;
  readonly save: SaveView;
  readonly interaction: InteractionView;
  readonly service: ServiceView;
  readonly rest: RestView;
  readonly conversation: ConversationView;
  readonly combat: CombatView;
  readonly progression: ProgressionView;
  readonly promotion: PromotionView;
  readonly skills: SkillsView;
  readonly magic: MagicView;
  readonly alchemy: AlchemyView;
  readonly equipment: EquipmentView;
  readonly character: CharacterView;
  readonly quests: QuestsView;
  readonly map: MapView;
  readonly journal: JournalView;
  readonly controls: ControlsView;
  /** The answer to the party's latest act. */
  readonly feedback: FeedbackView;
}

/** One projection as read: every block, or nothing when it names no session; and every problem met reading it. */
export interface Reading {
  readonly snapshot: SnapshotView | null;
  readonly problems: readonly string[];
}

/** Reads one projection value. */
export function readSnapshot(value: unknown): Reading {
  const problems: Problems = [];
  const root = fields(value, '', problems);
  const before = problems.length;
  const composition = readComposition(root.object('composition'));
  const session = readSession(root.object('session'));
  if (problems.length !== before) return { snapshot: null, problems };

  const read = <T>(key: string, reader: (entry: Fields) => T): T => block(root, key, reader, problems);
  const snapshot: SnapshotView = {
    completion: read('completion', readCompletion),
    composition,
    menu: read('menu', readMenu),
    session,
    world: read('world', readWorld),
    movement: read('movement', readMovement),
    clock: read('clock', readClock),
    party: read('party', readParty),
    creation: read('creation', readCreation),
    save: read('save', readSave),
    interaction: read('interaction', readInteraction),
    service: read('service', readService),
    rest: read('rest', readRest),
    conversation: read('conversation', readConversation),
    combat: read('combat', readCombat),
    progression: read('progression', readProgression),
    promotion: read('promotion', readPromotion),
    skills: read('skills', readSkills),
    magic: read('magic', readMagic),
    alchemy: read('alchemy', readAlchemy),
    equipment: read('equipment', readEquipment),
    character: read('character', readCharacter),
    quests: read('quests', readQuests),
    map: read('map', readMap),
    journal: read('journal', readJournal),
    controls: read('controls', readControls),
    feedback: read('feedback', readFeedback),
  };
  return { snapshot, problems };
}
