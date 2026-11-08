const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../../src/PlanForge.Web/wwwroot/ui.js'), 'utf8');

for (const openerSurvives of [true, false]) {
	test(`removed modal restores keyboard focus (opener survives: ${openerSurvives})`, () => {
		let callback, disconnected = false, focused;
		const opener = { isConnected: openerSurvives, focus: () => { focused = 'opener'; } };
		const selected = { focus: () => { focused = 'selected'; } };
		const dialog = { open: false, isConnected: true, showModal() { this.open = true; } };
		const context = { window: {}, document: { activeElement: opener, body: {}, querySelector: () => selected }, MutationObserver: class {
			constructor(handler) { callback = handler; }
			observe() {}
			disconnect() { disconnected = true; }
		} };
		vm.runInNewContext(source, context);
		context.window.planForge.openDialog(dialog);
		assert.equal(typeof callback, 'function', 'Modal removal must have a focus-restoration observer');
		dialog.isConnected = false;
		callback();
		assert.equal(focused, openerSurvives ? 'opener' : 'selected');
		assert.equal(disconnected, true);
	});
}

function contrast(first, second) {
	const luminance = hex => {
		const channels = hex.match(/[a-f\d]{2}/gi).map(value => parseInt(value, 16) / 255).map(value => value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4);
		return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
	};
	const values = [luminance(first), luminance(second)].sort((a, b) => a - b);
	return (values[1] + 0.05) / (values[0] + 0.05);
}

const cssPath = path.join(__dirname, '../../src/PlanForge.Web/wwwroot/app.css');

function declarations(selector) {
	const css = fs.readFileSync(cssPath, 'utf8');
	const blocks = [...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)];
	const block = blocks.find(match => match[1].trim() === selector && match[2].includes(':'));
	assert.ok(block, `Missing CSS rule ${selector}`);
	return Object.fromEntries([...block[2].matchAll(/([\w-]+)\s*:\s*([^;]+);/g)].map(match => [match[1], match[2].trim()]));
}

test('secondary and field interaction states stay coherent without decorative card hover', () => {
	assert.equal(declarations('.quiet:hover:not(:disabled)').background, 'var(--pf-control-hover)');
	assert.equal(declarations('.quiet:hover:not(:disabled)')['border-color'], 'var(--pf-primary)');
	assert.equal(declarations('.primary:hover:not(:disabled)')['border-color'], 'var(--pf-primary-hover)');
	const field = declarations('input:not([type=checkbox]):not([type=radio]):focus, select:focus, textarea:focus');
	assert.equal(field.background, 'var(--pf-input-focus)');
	assert.equal(field['border-color'], 'var(--pf-primary)');
	assert.equal(field.outline, '1px solid var(--pf-primary)');
	assert.equal(field['outline-offset'], '0');
	assert.equal(declarations('.sidebar nav a.active:hover').background, 'var(--pf-selection)');
	const css = fs.readFileSync(cssPath, 'utf8');
	assert.doesNotMatch(css, /\.project-card:hover\s*\{/);
	assert.doesNotMatch(css, /button:hover:not\(:disabled\)\s*\{/);
	assert.match(css, /input:-webkit-autofill/);
});

test('destructive plan and credential actions consume the existing common danger variant', () => {
	const root = path.join(__dirname, '../../src/PlanForge.Web/Components');
	for (const [file, marker] of [['Planning/WorkPlanInspector.razor', 'Delete'], ['Pages/ProjectDetail.razor', 'Clean completed phases'], ['Shared/AgentTokenRow.razor', 'Revoke'], ['Shared/AgentTokenPanel.razor', 'Delete revoked tokens']]) {
		const markup = fs.readFileSync(path.join(root, file), 'utf8');
		const buttons = markup.match(/<button\b[^>]*>[\s\S]*?<\/button>/g) || [];
		const button = buttons.find(button => button.includes(`T["${marker}"]`));
		assert.ok(button, `${file}: ${marker} exists`);
		assert.match(button, /class="(?:[^\"]*\s)?danger(?:\s[^\"]*)?"/, `${file}: ${marker} uses common danger`);
	}
});

function resolved(value) {
	assert.ok(value, 'Missing CSS value');
	return value.replace(/var\((--[\w-]+)\)/g, (_, token) => resolved(declarations(':root')[token]));
}

function color(selector, property) {
	return resolved(declarations(selector)[property]).match(/#[a-f\d]{6}\b/i)?.[0];
}

test('actual control boundaries meet non-text AA contrast against dark panels', () => {
	const border = color('input, select, textarea', 'border');
	const surface = declarations(':root')['--pf-surface'];
	assert.ok(contrast(border, surface) >= 3, `Input border contrast ${contrast(border, surface).toFixed(2)} must be at least 3:1`);
});

test('approved palette is applied through semantic tokens with accessible text and controls', () => {
	const tokens = declarations(':root');
	const expected = {
		'--pf-bg': '#111416',
		'--pf-surface': '#1D2123',
		'--pf-primary': '#D1BC9A',
		'--pf-primary-hover': '#E1CCAA',
		'--pf-selection': '#34312B',
		'--pf-text': '#E7E8E4'
	};
	for (const [token, hex] of Object.entries(expected))
		assert.equal(tokens[token]?.toUpperCase(), hex, `${token} must use the approved palette`);

	for (const token of ['--pf-bg', '--pf-surface', '--pf-surface-raised', '--pf-input-bg', '--pf-border']) {
		const channels = resolved(tokens[token]).match(/[a-f\d]{2}/gi).map(value => parseInt(value, 16));
		assert.ok(Math.max(...channels) - Math.min(...channels) <= 8, `${token} must remain near-neutral`);
	}
	for (const [selector, property, token] of [
		['a', 'color', '--pf-primary'],
		['.primary', 'background', '--pf-primary'],
		['.primary:hover:not(:disabled)', 'background', '--pf-primary-hover'],
		['.sidebar nav a.active', 'background', '--pf-selection'],
		['.plan-row.is-selected', 'background', '--pf-selection'],
		['.badge.active', 'color', '--pf-emerald']
	])
		assert.equal(declarations(selector)[property], `var(${token})`, `${selector} must consume ${token}`);

	const surfaces = ['--pf-bg', '--pf-surface', '--pf-surface-raised', '--pf-input-bg', '--pf-selection'];
	const assertContrast = (foreground, background, minimum, label) => {
		const ratio = contrast(foreground, background);
		assert.ok(ratio >= minimum, `${label}: ${ratio.toFixed(2)} must be at least ${minimum}:1`);
	};
	for (const background of surfaces) {
		for (const foreground of ['--pf-text', '--pf-muted', '--pf-primary', '--pf-emerald', '--pf-amber', '--pf-red'])
			assertContrast(resolved(tokens[foreground]), resolved(tokens[background]), 4.5, `${foreground} on ${background}`);
		for (const foreground of ['--pf-control-border', '--pf-focus'])
			assertContrast(resolved(tokens[foreground]), resolved(tokens[background]), 3, `${foreground} on ${background}`);
	}
	for (const selector of ['.primary', '.primary:hover:not(:disabled)', '.quiet', '.danger', '.danger:hover:not(:disabled)', '.badge.success', '.badge.active', '.badge.warning', '.badge.error', '.notice.error', '.notice.warning', '.toast']) {
		const base = selector.startsWith('.primary:') ? '.primary' : selector.startsWith('.danger:') ? '.danger' : selector;
		assertContrast(color(base, 'color'), color(selector, 'background'), 4.5, `${selector} text`);
	}
	for (const selector of ['input, select, textarea', '.quiet', '.danger']) {
		const border = color(selector, declarations(selector).border ? 'border' : 'border-color');
		assertContrast(border, color(selector, 'background'), 3, `${selector} inner boundary`);
		assertContrast(border, tokens['--pf-surface'], 3, `${selector} outer boundary`);
	}
	assert.equal(declarations(':focus-visible').outline, '2px solid var(--pf-focus)');
	assert.notEqual(tokens['--pf-emerald'], tokens['--pf-primary']);
	assert.notEqual(tokens['--pf-red'], tokens['--pf-primary']);
	assert.notEqual(tokens['--pf-amber'], tokens['--pf-primary']);
	assert.match(fs.readFileSync(cssPath, 'utf8'), /@media\(prefers-reduced-motion:reduce\)[^}]*animation: none !important; transition: none !important; scroll-behavior: auto !important;/);
});

test('canonical static symbol and favicon share the exact approved SVG bytes', () => {
	const expected = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" role="img" aria-labelledby="logoTitle">
<title id="logoTitle">PlanForge — refined forged P hammer mark</title>
<path d="M 19 17 H 81 C 105 17 117 31 117 49 C 117 68 103 82 81 82 H 58 V 109 C 58 113 55 116 51 116 H 41 C 37 116 34 113 34 109 V 42 H 19 C 15 42 12 39 12 35 V 24 C 12 20 15 17 19 17 Z M 58 39 V 63 H 81 C 91 63 97 58 97 51 C 97 44 91 39 81 39 Z" fill="#D1BC9A" fill-rule="evenodd"/>
</svg>
`;
	const canonical = fs.readFileSync(path.join(__dirname, '../../src/PlanForge.Web/wwwroot/favicon.svg'));
	assert.deepEqual(canonical, Buffer.from(expected, 'utf8'), 'The shared symbol must match the approved SVG byte for byte');
	const app = fs.readFileSync(path.join(__dirname, '../../src/PlanForge.Web/Components/App.razor'), 'utf8');
	assert.match(app, /<link\b[^>]*rel="icon"[^>]*href="favicon\.svg"[^>]*type="image\/svg\+xml"/);
	for (const layout of ['Layout/MainLayout.razor', 'Layout/AuthLayout.razor']) {
		const markup = fs.readFileSync(path.join(__dirname, '../../src/PlanForge.Web/Components', layout), 'utf8');
		assert.match(markup, /<img\b[^>]*class="brand-mark"[^>]*src="favicon\.svg"[^>]*alt=""/);
		assert.doesNotMatch(markup, /<span class="brand-mark">P<\/span>/);
	}
	const mark = declarations('.brand-mark');
	assert.equal(mark.width, '34px');
	assert.equal(mark.height, '34px');
	assert.equal(mark['object-fit'], 'contain');
	assert.equal(mark.background, undefined, 'The approved symbol must not be painted on the old colored tile');
});

test('single native selects reserve text space and inset the decorative arrow by 16px', () => {
	const select = declarations('select:not([multiple]):not([size]), select[size="1"]:not([multiple])');
	assert.equal(select.appearance, 'none');
	assert.equal(select['background-repeat'], 'no-repeat');
	assert.equal(resolved(select['background-position']), 'right 16px center');
	assert.equal(resolved(select['background-size']), '16px 16px');
	assert.equal(resolved(select['padding-inline-end']), 'calc(16px + 16px + 16px)');
	const svg = decodeURIComponent(select['background-image'].match(/^url\("data:image\/svg\+xml,([^\"]+)"\)$/)[1]);
	assert.match(svg, /viewBox='0 0 16 16'/);
	assert.match(svg, /fill='none'/);
	const arrowColor = svg.match(/stroke='(#[a-f\d]{6})'/i)[1];
	assert.ok(contrast(arrowColor, declarations(':root')['--pf-input-bg']) >= 3, 'Decorative arrow must remain distinguishable');
	const css = fs.readFileSync(cssPath, 'utf8');
	assert.match(css, /@media\(forced-colors:active\)\s*\{\s*select[^{}]+\{\s*appearance: auto;\s*background-image: none;\s*padding-inline-end: 12px;/);
});

test('profile navigation reserves stable inner spacing and uses semantic active and hover states', () => {
	const normal = declarations('.account-link');
	assert.equal(normal.border, '1px solid transparent');
	assert.equal(normal.padding, '11px 14px');
	assert.equal(normal.gap, '8px');
	assert.equal(declarations('.account-link:hover')['text-decoration'], 'none');
	assert.equal(declarations('.account-link:hover').background, declarations('.sidebar nav a:hover').background);
	assert.equal(declarations('.account-link.active').background, 'var(--pf-selection)');
	assert.equal(declarations('.account-link.active')['border-color'], 'var(--pf-primary)');
	assert.equal(declarations('.breadcrumb')['min-width'], '0');
	assert.equal(declarations('.breadcrumb ol')['list-style'], 'none');
});

test('inspector sticks only when its real height fits a wide viewport', () => {
	let height = 300;
	let sticky;
	const inspector = { getBoundingClientRect: () => ({ height }), classList: { toggle: (name, enabled) => { assert.equal(name, 'can-stick'); sticky = enabled; } } };
	const context = { window: {}, innerWidth: 1400, innerHeight: 800, document: { getElementById: () => inspector } };
	vm.runInNewContext(source, context);
	assert.equal(typeof context.window.planForge.layoutInspector, 'function');
	context.window.planForge.layoutInspector();
	assert.equal(sticky, true);
	height = 900;
	context.window.planForge.layoutInspector();
	assert.equal(sticky, false);
	height = 300; context.innerWidth = 800;
	context.window.planForge.layoutInspector();
	assert.equal(sticky, false);
});

test('manual narrow-layout selection reveals offscreen inspector without an inner scroll', () => {
	let options;
	const inspector = { classList: { contains: () => false }, getBoundingClientRect: () => ({ top: 1200, bottom: 1600 }), scrollIntoView: value => { options = value; } };
	const context = { window: {}, innerHeight: 800, matchMedia: () => ({ matches: true }), document: { getElementById: () => inspector } };
	vm.runInNewContext(source, context);
	assert.equal(typeof context.window.planForge.revealInspector, 'function');
	context.window.planForge.revealInspector();
	assert.equal(options.block, 'start');
	assert.equal(options.behavior, 'instant');
});

test('registration validates exact confirmation before submit without replacing native required messages', () => {
	const context = { window: {} };
	vm.runInNewContext(source, context);
	let focused = false;
	const password = { value: 'CorrectPassword123!' };
	const confirmation = { value: 'CorrectPassword123! ', setAttribute: () => {}, removeAttribute: () => {}, focus: () => { focused = true; } };
	const error = { hidden: true };
	const form = { elements: { namedItem: name => name === 'password' ? password : confirmation }, querySelector: () => error };
	assert.equal(typeof context.window.planForge.validateRegistration, 'function');
	assert.equal(context.window.planForge.validateRegistration(form), false);
	assert.equal(error.hidden, false);
	assert.equal(focused, true);
	confirmation.value = password.value;
	assert.equal(context.window.planForge.validateRegistration(form), true);
	assert.equal(error.hidden, true);
	confirmation.value = '';
	assert.equal(context.window.planForge.validateRegistration(form), false);
});

test('registration listeners enforce confirmation under the existing no-inline-script CSP', () => {
	const listeners = {};
	const context = { window: {}, document: { addEventListener: (name, callback) => { listeners[name] = callback; } } };
	vm.runInNewContext(source, context);
	assert.equal(typeof listeners.submit, 'function');
	let prevented = false;
	context.window.planForge.validateRegistration = () => false;
	listeners.submit({ target: { matches: () => true }, preventDefault: () => { prevented = true; } });
	assert.equal(prevented, true);
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
