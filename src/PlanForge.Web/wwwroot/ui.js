window.planForge = {
	formatStatusDate: iso => {
		const date = new Date(iso);
		const day = new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", year: "numeric" }).format(date);
		const time = new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit", hourCycle: "h23" }).format(date);
		return `${day} · ${time}`;
	},
	revealInspector: () => {
		const inspector = document.getElementById("plan-inspector");
		if (!inspector || inspector.classList.contains("can-stick")) return;
		const bounds = inspector.getBoundingClientRect();
		if (bounds.top >= 64 && bounds.bottom <= innerHeight) return;
		inspector.scrollIntoView({ block: "start", behavior: matchMedia("(prefers-reduced-motion: reduce)").matches ? "instant" : "smooth" });
	},
	layoutInspector: () => {
		const inspector = document.getElementById("plan-inspector");
		if (!inspector) return;
		inspector.classList.toggle("can-stick", innerWidth > 1100 && inspector.getBoundingClientRect().height <= innerHeight - 96);
	},
	validateRegistration: form => {
		const confirmation = form.elements.namedItem("confirmPassword");
		if (!confirmation) return true;
		const valid = confirmation.value.length > 0 && form.elements.namedItem("password").value === confirmation.value;
		const error = form.querySelector("#password-confirmation-error");
		if (error) error.hidden = valid;
		if (valid) {
			confirmation.removeAttribute("aria-invalid");
			confirmation.setAttribute("aria-describedby", "password-confirmation-help");
		} else {
			confirmation.setAttribute("aria-invalid", "true");
			confirmation.setAttribute("aria-describedby", "password-confirmation-help password-confirmation-error");
			confirmation.focus();
		}
		return valid;
	},
	openDialog: element => {
		if (element.open) return;
		const opener = document.activeElement;
		element.showModal();
		// Blazor removes conditional dialogs instead of calling close(); restore focus after that DOM change.
		const observer = new MutationObserver(() => {
			if (element.isConnected) return;
			observer.disconnect();
			const target = opener?.isConnected && opener !== document.body ? opener : document.querySelector('.work-plan-tree [aria-current="true"]') ?? document.querySelector('main');
			target?.focus({ preventScroll: true });
		});
		observer.observe(document.body, { childList: true, subtree: true });
	},
	revealCurrentWork: id => {
		const heading = document.querySelector(`[data-item-id="${CSS.escape(id)}"] > .node-heading`);
		if (!heading) return;
		const bounds = heading.getBoundingClientRect();
		if (bounds.top >= 64 && bounds.bottom <= innerHeight) return;
		heading.scrollIntoView({ block: "nearest", behavior: matchMedia("(prefers-reduced-motion: reduce)").matches ? "instant" : "smooth" });
	},
	download: async (url) => {
		const response = await fetch(url, { credentials: "same-origin" });
		if (!response.ok) return { success: false, status: response.status };
		if (response.redirected || (response.headers.get("Content-Type") || "").startsWith("text/html"))
			return { success: false, status: 401 };
		const blob = await response.blob();
		const disposition = response.headers.get("Content-Disposition") || "";
		const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i);
		const plain = disposition.match(/filename="?([^";]+)"?/i);
		const name = encoded ? decodeURIComponent(encoded[1]) : plain ? plain[1] : "download";
		const anchor = document.createElement("a");
		const objectUrl = URL.createObjectURL(blob);
		anchor.href = objectUrl; anchor.download = name; anchor.click();
		setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
		return { success: true, status: response.status };
	}
};
// Delegation survives enhanced navigation and obeys script-src 'self'; native required validation runs first.
if (typeof document !== "undefined" && document.addEventListener) {
	document.addEventListener("toggle", event => {
		if (event.target.closest("#plan-inspector")) window.planForge.layoutInspector();
	}, true);
	document.addEventListener("submit", event => {
		if (event.target.matches('form[data-registration="true"]') && !window.planForge.validateRegistration(event.target))
			event.preventDefault();
	}, true);
}
if (window.addEventListener) window.addEventListener("resize", () => window.planForge.layoutInspector());
