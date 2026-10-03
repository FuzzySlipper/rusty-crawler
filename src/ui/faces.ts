/**
 * The row of member faces a book opens with: the party's members in roster order, the one the book shows outlined,
 * and a click selecting another through the party's own selection, so the adventure bar's outline follows.
 */

import type { Claim } from './actions.js';
import type { RosterMemberView } from './overview.js';
import { ACTIONS } from './actions.js';
import { button, element } from './dom.js';

/** The member a book shows: the party's selected member, or the first when nobody is selected. */
export function shownMember(roster: readonly RosterMemberView[]): { readonly member: RosterMemberView | undefined; readonly index: number } {
  const member = roster.find((entry) => entry.selected) ?? roster[0];
  return { member, index: member === undefined ? -1 : roster.indexOf(member) };
}

/** The faces, each a button that selects its member. */
export function memberFaces(claim: Claim, roster: readonly RosterMemberView[], shown: RosterMemberView | undefined): HTMLElement[] {
  return roster.map((entry) => {
    const face = button('', 'crawler-character-face');
    face.dataset.member = entry.member;
    face.dataset.selected = entry === shown ? 'yes' : 'no';
    face.title = entry.name;
    if (entry.portraitImage !== '') {
      const picture = element('img');
      picture.src = entry.portraitImage;
      picture.alt = entry.name;
      face.append(picture);
    } else {
      face.textContent = entry.name.charAt(0);
    }

    face.addEventListener('click', () => claim(ACTIONS.partySelectMember, { member: entry.member }));
    return face;
  });
}
