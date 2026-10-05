import assert from 'node:assert/strict';
import { test } from 'node:test';
import { harness } from './harness.mjs';
import { mountCompletion } from '../../src/ui/generated/completion.js';

test('earned ending opens, can be dismissed and recalled, and reopens on a fresh session', () => {
  const h = harness();
  try {
    let focused = 0;
    const view = mountCompletion(() => focused++);
    h.root.append(view.element, view.recall);
    const ending = { completed: true, id: 'light', title: 'A Door to the Stars', text: 'The expedition is complete.', continues: true };
    view.render(ending, false);
    assert.equal(view.element.hidden, false);
    assert.equal(view.element.querySelector('h1').textContent, ending.title);
    view.element.querySelector('button').click();
    assert.equal(focused, 1);
    view.render(ending, false);
    assert.equal(view.element.hidden, true);
    view.recall.click();
    assert.equal(view.element.hidden, false);
    view.render(ending, true);
    assert.equal(view.element.hidden, true);
    view.clear();
    assert.equal(view.recall.hidden, true);
    view.render(ending, false);
    assert.equal(view.element.hidden, false);
    view.render({ ...ending, id: 'dark', title: 'Masters of a New Age' }, false);
    assert.equal(view.element.querySelector('h1').textContent, 'Masters of a New Age');
    view.render({ completed: false, id: '', title: '', text: '', continues: true }, false);
    assert.equal(view.element.hidden, true);
    assert.equal(view.recall.hidden, true);
  } finally { h.restore(); }
});
