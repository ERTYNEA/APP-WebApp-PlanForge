window.planForge = {
	openDialog: element => { if (!element.open) element.showModal(); },
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
