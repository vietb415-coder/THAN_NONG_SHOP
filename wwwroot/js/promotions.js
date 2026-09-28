(() => {
  const page = document.getElementById("promotionPage");
  if (!page) return;

  const wheel = document.getElementById("luckyWheel");
  const spinButton = document.getElementById("spinButton");
  const status = document.getElementById("spinStatus");
  const modal = document.getElementById("rewardModal");
  const storageKey = `thanNongLuckySpin:${page.dataset.member}`;
  const today = new Date().toLocaleDateString("en-CA");
  let spinning = false;
  let currentCode = "";

  const previous = (() => { try { return JSON.parse(localStorage.getItem(storageKey)); } catch { return null; } })();
  if (page.dataset.authenticated === "true" && previous?.date === today) {
    spinButton.disabled = true;
    status.innerHTML = `Hôm nay bạn đã nhận <b>${previous.title}</b>`;
  }

  function showReward(prize) {
    document.getElementById("rewardTitle").textContent = prize.title;
    document.getElementById("rewardDescription").textContent = prize.description;
    document.getElementById("rewardCode").textContent = prize.code;
    currentCode = prize.code;
    modal.hidden = false;
    document.body.classList.add("modal-open");
  }

  spinButton.addEventListener("click", async () => {
    if (spinning || spinButton.disabled) return;
    spinning = true;
    spinButton.disabled = true;
    status.textContent = "Vòng quay đang chọn món quà cho bạn…";
    let prize;
    const requestController = new AbortController();
    const requestTimeout = window.setTimeout(() => requestController.abort(), 10000);
    try {
      const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
      const response = await fetch(page.dataset.spinUrl, {
        method: "POST",
        headers: { "RequestVerificationToken": token, "X-Requested-With": "XMLHttpRequest" },
        signal: requestController.signal
      });
      const data = await response.json();
      if (!response.ok) throw new Error(data.message || "Không thể thực hiện lượt quay.");
      prize = data;
    } catch (error) {
      status.textContent = error.name === "AbortError"
        ? "Máy chủ phản hồi quá lâu. Vui lòng bấm quay lại."
        : (error.message || "Có lỗi xảy ra. Vui lòng thử lại.");
      spinning = false;
      spinButton.disabled = false;
      return;
    } finally {
      window.clearTimeout(requestTimeout);
    }
    const index = prize.index;
    const segment = 360 / 8;
    const degrees = (360 * 6) + (360 - (index * segment + segment / 2));
    wheel.style.transform = `rotate(${degrees}deg)`;
    spinButton.style.transform = `translate(-50%, -50%) rotate(${-degrees}deg)`;
    window.setTimeout(() => {
      localStorage.setItem(storageKey, JSON.stringify({ date: today, ...prize }));
      status.innerHTML = `Chúc mừng! Bạn nhận được <b>${prize.title}</b>`;
      spinning = false;
      showReward(prize);
    }, 4600);
  });

  async function copyCode(code, button) {
    try { await navigator.clipboard.writeText(code); }
    catch {
      const input = document.createElement("textarea"); input.value = code; document.body.appendChild(input); input.select(); document.execCommand("copy"); input.remove();
    }
    const original = button.textContent; button.textContent = "Đã sao chép ✓"; button.classList.add("is-copied");
    window.setTimeout(() => { button.textContent = original; button.classList.remove("is-copied"); }, 1800);
  }
  document.querySelectorAll("[data-claim-code]").forEach(button => button.addEventListener("click", async () => {
    if (page.dataset.authenticated !== "true") { window.location.href = page.dataset.loginUrl; return; }
    const original = button.textContent;
    button.disabled = true; button.textContent = "Đang tạo mã…";
    try {
      const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
      const body = new URLSearchParams({ templateCode: button.dataset.claimCode });
      const response = await fetch(page.dataset.claimUrl, {
        method: "POST",
        headers: { "RequestVerificationToken": token, "Content-Type": "application/x-www-form-urlencoded" },
        body
      });
      const data = await response.json();
      if (!response.ok) throw new Error(data.message || "Không thể tạo voucher.");
      button.closest(".voucher-card").querySelector("code").textContent = data.code;
      await copyCode(data.code, button);
    } catch (error) {
      button.textContent = error.message || "Thử lại";
    } finally {
      window.setTimeout(() => { button.disabled = false; if (!button.classList.contains("is-copied")) button.textContent = original; }, 1900);
    }
  }));
  document.getElementById("copyReward").addEventListener("click", event => copyCode(currentCode, event.currentTarget));
  modal.querySelectorAll("[data-close-reward]").forEach(element => element.addEventListener("click", () => { modal.hidden = true; document.body.classList.remove("modal-open"); }));
  document.addEventListener("keydown", event => { if (event.key === "Escape" && !modal.hidden) { modal.hidden = true; document.body.classList.remove("modal-open"); } });

  const campaignEnd = new Date("2026-10-01T00:00:00+07:00").getTime();
  function updateCountdown() {
    let remaining = Math.max(0, campaignEnd - Date.now());
    const values = [Math.floor(remaining / 86400000), Math.floor(remaining / 3600000) % 24, Math.floor(remaining / 60000) % 60, Math.floor(remaining / 1000) % 60];
    ["countdownDays", "countdownHours", "countdownMinutes", "countdownSeconds"].forEach((id, i) => document.getElementById(id).textContent = String(values[i]).padStart(2, "0"));
  }
  updateCountdown(); window.setInterval(updateCountdown, 1000);
})();
