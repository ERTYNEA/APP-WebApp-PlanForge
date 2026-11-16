const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../../src/PlanForge.Web/wwwroot/ui.js'), 'utf8');

function createDialogFixture() {
	let removal;
	const document = { body: {}, activeElement: null, querySelector: () => null };
	const opener = { isConnected: true, focus() { document.activeElement = this; } };
	const cancel = { focus() { document.activeElement = this; } };
	const ordinaryInput = { focus() { document.activeElement = this; } };
	const dialog = {
		open: false, isConnected: true,
		showModal() { this.open = true; ordinaryInput.focus(); },
		querySelector(selector) { assert.equal(selector, '#confirm-cancel'); return cancel; }
	};
	document.activeElement = opener;
	const context = { window: {}, document, MutationObserver: class {
		constructor(callback) { removal = callback; }
		observe() {}
		disconnect() {}
	} };
	vm.runInNewContext(source, context);
	return { api: context.window.planForge, document, opener, cancel, ordinaryInput, dialog, remove() { dialog.isConnected = false; removal(); } };
}

test('confirmation explicitly focuses Cancel after native showModal', () => {
	const fixture = createDialogFixture();
	fixture.api.openDialog(fixture.dialog, '#confirm-cancel');
	assert.equal(fixture.document.activeElement, fixture.cancel);
	fixture.remove();
	assert.equal(fixture.document.activeElement, fixture.opener);
});

test('ordinary modal retains native input autofocus', () => {
	const fixture = createDialogFixture();
	fixture.api.openDialog(fixture.dialog);
	assert.equal(fixture.document.activeElement, fixture.ordinaryInput);
	fixture.remove();
	assert.equal(fixture.document.activeElement, fixture.opener);
});

test('rerender of an already-open confirmation does not steal focus', () => {
	const fixture = createDialogFixture();
	fixture.dialog.open = true;
	fixture.api.openDialog(fixture.dialog, '#confirm-cancel');
	assert.equal(fixture.document.activeElement, fixture.opener);
});
