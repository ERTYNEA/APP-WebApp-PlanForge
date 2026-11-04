const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../../src/PlanForge.Web/wwwroot/ui.js'), 'utf8');

function contrast(first, second) {
	const luminance = hex => {
		const channels = hex.match(/[a-f\d]{2}/gi).map(value => parseInt(value, 16) / 255).map(value => value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4);
		return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
	};
	const values = [luminance(first), luminance(second)].sort((a, b) => a - b);
	return (values[1] + 0.05) / (values[0] + 0.05);
}

test('actual control boundaries meet non-text AA contrast against dark panels', () => {
	const css = fs.readFileSync(path.join(__dirname, '../../src/PlanForge.Web/wwwroot/app.css'), 'utf8');
	const controls = css.match(/input, select, textarea \{(?=[^}]*border:)[^}]+/)[0];
	const border = controls.match(/border: 1px solid (#[a-f\d]+)/i)[1];
	const surface = css.match(/--pf-surface: (#[a-f\d]+)/i)[1];
	assert.ok(contrast(border, surface) >= 3, `Input border contrast ${contrast(border, surface).toFixed(2)} must be at least 3:1`);
});

test('download rejects a login redirect instead of downloading HTML as a successful export', async () => {
	let clicked = false;
	const context = {
		window: {},
		fetch: async () => ({ ok: true, status: 200, redirected: true, headers: new Headers({ 'Content-Type': 'text/html' }), blob: async () => new Blob(['Sign in']) }),
		document: { createElement: () => ({ click: () => { clicked = true; } }) },
		URL: { createObjectURL: () => 'blob:test', revokeObjectURL: () => {} },
		setTimeout: () => {}
	};
	vm.runInNewContext(source, context);
	const result = await context.window.planForge.download('/files/projects/project');
	assert.equal(result.success, false);
	assert.equal(result.status, 401);
	assert.equal(clicked, false);
});

for (const reducedMotion of [false, true]) {
	test(`current work only scrolls offscreen content, reduced motion ${reducedMotion}`, () => {
		let bounds = { top: 100, bottom: 160 };
		let options;
		const context = {
			window: {}, innerHeight: 800,
			CSS: { escape: value => value },
			matchMedia: () => ({ matches: reducedMotion }),
			document: { querySelector: () => ({ getBoundingClientRect: () => bounds, scrollIntoView: value => { options = value; } }) }
		};
		vm.runInNewContext(source, context);
		context.window.planForge.revealCurrentWork('item-id');
		assert.equal(options, undefined);
		bounds = { top: 1000, bottom: 1060 };
		context.window.planForge.revealCurrentWork('item-id');
		assert.equal(options.block, 'nearest');
		assert.equal(options.behavior, reducedMotion ? 'instant' : 'smooth');
	});
}
