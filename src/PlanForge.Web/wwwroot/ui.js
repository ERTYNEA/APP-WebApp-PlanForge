window.planForge = {
	openDialog: element => { if (!element.open) element.showModal(); },
	download: async (url) => {
		const response = await fetch(url, { credentials: "same-origin" });
		if (!response.ok) return { success: false, status: response.status };
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
