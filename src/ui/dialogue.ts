/**
 * The dialogue screen: the person spoken with, their face and greeting, the others present to turn to, the topics
 * that may be brought up and the ones withheld with why, what has been said, and the way out — the house-screen shape
 * of the original, with the speaker beside their words.
 *
 * Every line, topic and reason is the conversation mechanism's own; a topic that hands the party to a counter opens the
 * counter screen because the product opened it.
 */

import type { ConversationPersonView, ConversationReading } from './conversation.js';
import { ACTIONS } from './actions.js';
import { button, element, redrawGuard, report, result, section, type Host, type Section } from './dom.js';

/** A face: the portrait the Engine serves, or the person's initial where there is none. */
function face(person: ConversationPersonView, className: string): HTMLElement {
  if (person.portraitImage !== '') {
    const picture = element('img', className);
    picture.src = person.portraitImage;
    picture.alt = person.name;
    return picture;
  }

  const letter = element('span', `${className} crawler-dialogue-initial`);
  letter.textContent = person.name.charAt(0);
  return letter;
}

/** Mounts the dialogue screen. */
export function mountDialogue(host: Host): Section<ConversationReading> {
  const { claim } = host;
  const dialogue = section('crawler-dialogue');
  const speaker = element('div', 'crawler-dialogue-speaker');
  const words = element('div', 'crawler-dialogue-words');
  const outcome = result('crawler-dialogue-outcome');
  const said = element('div', 'crawler-dialogue-said');
  const topics = element('div', 'crawler-dialogue-topics');
  const withheld = element('ul', 'crawler-dialogue-withheld');
  const others = element('div', 'crawler-dialogue-others');
  const thieves = element('div', 'crawler-dialogue-thieves');
  const actions = element('div', 'crawler-actions');
  const leave = button('Take your leave', 'crawler-dialogue-leave');
  leave.addEventListener('click', () => {
    if (leave.dataset.action !== undefined && leave.dataset.action !== '') claim(leave.dataset.action);
  });
  actions.append(leave);
  words.append(said, topics, withheld, thieves, actions);
  const layout = element('div', 'crawler-dialogue-layout');
  layout.append(speaker, words);
  dialogue.append(outcome, layout);
  const changed = redrawGuard();

  const render = ({ conversation: view, leave: control }: ConversationReading): void => {
    dialogue.hidden = !view.available || !view.open;
    dialogue.dataset.subject = view.subject;
    report(outcome, view.outcome, view.code, view.residue === '' ? view.message : `${view.message} ${view.residue}`);
    leave.disabled = !control.enabled;
    leave.dataset.action = control.action;
    if (!changed(view) || !view.open) return;

    // The speaker: their face, their name and the greeting they gave; the others present, each a face to turn to.
    const current = view.people.find((person) => person.speaking);
    const name = element('p', 'crawler-dialogue-name');
    name.textContent = view.speaker;
    const greeting = element('p', 'crawler-dialogue-greeting');
    greeting.textContent = view.greeting;
    others.replaceChildren(
      ...view.people
        .filter((person) => !person.speaking)
        .map((person) => {
          const turn = button('', 'crawler-dialogue-other');
          turn.dataset.person = person.id;
          turn.title = `Turn to ${person.name}`;
          const label = element('span');
          label.textContent = person.name;
          turn.append(face(person, 'crawler-dialogue-other-face'), label);
          turn.addEventListener('click', () => claim(ACTIONS.conversationPerson, { target: person.id }));
          return turn;
        }),
    );
    speaker.replaceChildren(
      ...(current === undefined ? [] : [face(current, 'crawler-dialogue-face')]),
      name,
      greeting,
      ...(others.childElementCount > 0 ? [others] : []),
    );

    // What has been said while the conversation has been open, oldest first.
    said.replaceChildren(
      ...view.said.map((line) => {
        const item = element('p', 'crawler-dialogue-line');
        item.dataset.speaker = line.speaker;
        const who = view.people.find((person) => person.id === line.speaker)?.name ?? '';
        // A line's residue — what the answer could not deliver — stays with the line it belongs to.
        const said = line.residue === '' ? line.text : `${line.text} ${line.residue}`;
        item.textContent = who === '' ? said : `${who}: ${said}`;
        return item;
      }),
    );

    // The topics that may be brought up, as the speaker's choices; the ones the state withholds, with why.
    topics.replaceChildren(
      ...view.topics.map((topic) => {
        const choice = button(topic.label, 'crawler-dialogue-topic');
        choice.dataset.id = topic.id;
        choice.disabled = !topic.available;
        choice.addEventListener('click', () => claim(ACTIONS.conversationTopic, { target: topic.id }));
        return choice;
      }),
    );
    withheld.replaceChildren(
      ...view.withheld.map((topic) => {
        const item = element('li');
        item.dataset.id = topic.id;
        item.textContent = `${topic.label} — ${topic.reason}`;
        return item;
      }),
    );
    thieves.replaceChildren(
      ...view.thieves.map((thief) => {
        const steal = button(`${thief.name}: pick their pocket`, 'crawler-dialogue-steal');
        steal.dataset.member = String(thief.index);
        steal.addEventListener('click', () => claim(ACTIONS.conversationSteal, { member: thief.index }));
        return steal;
      }),
    );
  };

  return { element: dialogue, render };
}
