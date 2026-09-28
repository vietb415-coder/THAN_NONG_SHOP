// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Customer-care chatbot widget.
(() => {
  const root = document.getElementById("aiChat");
  if (!root) return;

  const launcher = document.getElementById("chatLauncher");
  const panel = document.getElementById("chatPanel");
  const close = document.getElementById("chatClose");
  const form = document.getElementById("chatForm");
  const input = document.getElementById("chatInput");
  const messages = document.getElementById("chatMessages");
  const history = [];
  let conversationId = sessionStorage.getItem("thanNongChatConversation");
  let sending = false;
  let currentLanguage = "vi";
  const translations = {
    vi: { launcher:"Tư vấn AI",title:"Trợ lý Thần Nông",ready:"Sẵn sàng hỗ trợ",welcome:"Xin chào! Mình có thể giúp bạn chọn nông sản, tra cứu thông tin giao hàng hoặc kết nối với nhân viên tư vấn.",products:"Sản phẩm có sẵn",shipping:"Giao hàng",staff:"Gặp nhân viên",placeholder:"Nhập câu hỏi...",notice:"AI có thể nhầm lẫn. Không gửi mật khẩu hoặc thông tin thanh toán.",typing:"Đang tìm thông tin…",helpful:"Phản hồi này hữu ích?",thanks:"Cảm ơn bạn đã đánh giá!",view:"Xem",add:"Thêm vào giỏ",stock:"Còn",order:"Đơn",confirm:name=>`Thêm 1 ${name} vào giỏ hàng?`,error:"Xin lỗi, hệ thống đang bận. Bạn vui lòng thử lại hoặc liên hệ nhân viên qua các kênh bên dưới nhé.",prompts:{products:"Sản phẩm nào đang có sẵn?",shipping:"Chính sách giao hàng thế nào?",staff:"Tôi cần gặp nhân viên tư vấn"}},
    en: { launcher:"AI support",title:"Thần Nông Assistant",ready:"Ready to help",welcome:"Hello! I can help you choose products, check delivery information, or contact a staff member.",products:"Available products",shipping:"Shipping",staff:"Contact staff",placeholder:"Type your question...",notice:"AI can make mistakes. Do not send passwords or payment information.",typing:"Finding information…",helpful:"Was this helpful?",thanks:"Thank you for your feedback!",view:"View",add:"Add to cart",stock:"In stock",order:"Order",confirm:name=>`Add 1 ${name} to your cart?`,error:"Sorry, the system is busy. Please try again or contact staff through the channels below.",prompts:{products:"What products do you have?",shipping:"What is your shipping policy?",staff:"I need to contact a staff member"}},
    zh: { launcher:"AI 咨询",title:"神农助手",ready:"随时为您服务",welcome:"您好！我可以帮助您选择商品、查询配送信息或联系工作人员。",products:"可选商品",shipping:"配送",staff:"联系工作人员",placeholder:"请输入问题...",notice:"AI 可能会出错。请勿发送密码或付款信息。",typing:"正在查询信息…",helpful:"此回答有帮助吗？",thanks:"感谢您的评价！",view:"查看",add:"加入购物车",stock:"库存",order:"订单",confirm:name=>`将 1 件 ${name} 加入购物车？`,error:"抱歉，系统正忙。请稍后重试或通过下方渠道联系工作人员。",prompts:{products:"你们有什么产品？",shipping:"配送政策是什么？",staff:"我需要联系工作人员"}},
    ko: { launcher:"AI 상담",title:"Thần Nông 도우미",ready:"상담 가능",welcome:"안녕하세요! 상품 선택, 배송 정보 확인 또는 직원 연결을 도와드릴 수 있습니다.",products:"판매 상품",shipping:"배송",staff:"직원 연결",placeholder:"질문을 입력하세요...",notice:"AI는 실수할 수 있습니다. 비밀번호나 결제 정보를 보내지 마세요.",typing:"정보를 찾는 중…",helpful:"답변이 도움이 되었나요?",thanks:"평가해 주셔서 감사합니다!",view:"보기",add:"장바구니 담기",stock:"재고",order:"주문",confirm:name=>`${name} 1개를 장바구니에 담을까요?`,error:"죄송합니다. 시스템이 사용 중입니다. 다시 시도하거나 아래 채널로 직원에게 문의해 주세요.",prompts:{products:"어떤 제품을 판매하나요?",shipping:"배송 정책은 어떻게 되나요?",staff:"직원과 상담하고 싶어요"}},
    ja: { launcher:"AI相談",title:"Thần Nông アシスタント",ready:"対応可能",welcome:"こんにちは！商品のご案内、配送情報の確認、スタッフへの連絡をお手伝いします。",products:"取扱商品",shipping:"配送",staff:"スタッフへ連絡",placeholder:"質問を入力してください...",notice:"AIは誤る場合があります。パスワードや決済情報を送信しないでください。",typing:"情報を確認しています…",helpful:"この回答は役に立ちましたか？",thanks:"評価ありがとうございます！",view:"見る",add:"カートに追加",stock:"在庫",order:"注文",confirm:name=>`${name}を1点カートに追加しますか？`,error:"申し訳ありません。システムが混み合っています。再試行するか、下記の窓口からスタッフへご連絡ください。",prompts:{products:"どんな商品がありますか？",shipping:"配送ポリシーを教えてください",staff:"スタッフに連絡したいです"}}
  };

  const detectLanguage = text => /[가-힣]/.test(text) ? "ko" : /[ぁ-ヿ]/.test(text) ? "ja" : /[㐀-鿿]/.test(text) ? "zh" : /\b(the|what|how|product|order|shipping|hello|please|payment|delivery|price|help|thanks?)\b/i.test(text) ? "en" : "vi";
  const t = key => translations[currentLanguage][key];
  function setLanguage(language) {
    currentLanguage = translations[language] ? language : "vi";
    root.lang = currentLanguage;
    root.querySelectorAll("[data-i18n]").forEach(element => element.textContent = t(element.dataset.i18n));
    input.placeholder = t("placeholder");
    root.querySelectorAll("[data-chat-action]").forEach(button => {
      button.dataset.chatPrompt = translations[currentLanguage].prompts[button.dataset.chatAction];
    });
  }
  const browserLanguage = (navigator.language || "vi").toLowerCase();
  setLanguage(browserLanguage.startsWith("ko") ? "ko" : browserLanguage.startsWith("ja") ? "ja" :
    browserLanguage.startsWith("zh") ? "zh" : browserLanguage.startsWith("en") ? "en" : "vi");

  const toggle = (open) => {
    panel.hidden = !open;
    launcher.setAttribute("aria-expanded", String(open));
    if (open) setTimeout(() => input.focus(), 50);
  };
  launcher.addEventListener("click", () => toggle(panel.hidden));
  close.addEventListener("click", () => toggle(false));

  function addMessage(content, role, pending = false, messageId = null) {
    const wrap = document.createElement("div");
    wrap.className = `ai-message-wrap ai-message-wrap--${role}`;
    const item = document.createElement("div");
    item.className = `ai-message ai-message--${role}${pending ? " ai-message--typing" : ""}`;
    item.textContent = content;
    wrap.appendChild(item);
    if (role === "bot" && messageId) {
      const feedback = document.createElement("div");
      feedback.className = "ai-chat__feedback";
      feedback.innerHTML = `<span>${t("helpful")}</span><button type="button" aria-label="Helpful">👍</button><button type="button" aria-label="Not helpful">👎</button>`;
      feedback.querySelectorAll("button").forEach((button, index) => button.addEventListener("click", async () => {
        await postJson("/api/chat/feedback", { messageId, helpful: index === 0 });
        feedback.textContent = t("thanks");
      }));
      wrap.appendChild(feedback);
    }
    messages.appendChild(wrap);
    messages.scrollTop = messages.scrollHeight;
    return wrap;
  }

  function postJson(url, body) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    return fetch(url, { method: "POST", headers: { "Content-Type": "application/json", "RequestVerificationToken": token || "" }, body: JSON.stringify(body) });
  }

  function addProducts(products) {
    if (!products?.length) return;
    const list = document.createElement("div"); list.className = "ai-chat__cards";
    products.forEach(product => {
      const card = document.createElement("article"); card.className = "ai-product-card";
      if (product.imageUrl) { const img = document.createElement("img"); img.src = product.imageUrl; img.alt = ""; card.appendChild(img); }
      const info = document.createElement("div");
      const name = document.createElement("strong"); name.textContent = product.name;
      const meta = document.createElement("small"); meta.textContent = `${Number(product.price).toLocaleString("vi-VN")}đ · ${t("stock")} ${product.stock}`;
      const actions = document.createElement("div");
      const view = document.createElement("a"); view.href = `/Products/Details/${product.id}`; view.textContent = t("view");
      const add = document.createElement("button"); add.type = "button"; add.textContent = t("add");
      add.addEventListener("click", () => {
        if (!confirm(t("confirm")(product.name))) return;
        const form = document.createElement("form"); form.method = "post"; form.action = "/Cart/AddToCart";
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.cloneNode(true);
        form.innerHTML = `<input type="hidden" name="productId" value="${product.id}"><input type="hidden" name="quantity" value="1">`;
        if (token) form.appendChild(token); document.body.appendChild(form); form.submit();
      });
      actions.append(view, add); info.append(name, meta, actions); card.appendChild(info); list.appendChild(card);
    });
    messages.appendChild(list); messages.scrollTop = messages.scrollHeight;
  }

  function addOrders(orders) {
    if (!orders?.length) return;
    const list = document.createElement("div"); list.className = "ai-chat__orders";
    orders.forEach(order => { const item = document.createElement("div"); item.textContent = `${t("order")} #${order.id} · ${order.status} · ${Number(order.totalPrice).toLocaleString("vi-VN")}đ`; list.appendChild(item); });
    messages.appendChild(list);
  }

  async function send(text) {
    text = text.trim();
    if (!text || sending) return;
    setLanguage(detectLanguage(text));
    sending = true;
    addMessage(text, "user");
    const priorHistory = history.slice(-8);
    history.push({ role: "user", content: text });
    input.value = "";
    const typing = addMessage(t("typing"), "bot", true);
    try {
      const response = await postJson(root.dataset.endpoint, { message: text, history: priorHistory, conversationId });
      if (!response.ok) throw new Error("Chat request failed");
      const data = await response.json();
      typing.remove();
      conversationId = data.conversationId; sessionStorage.setItem("thanNongChatConversation", conversationId);
      addMessage(data.reply, "bot", false, data.messageId);
      addProducts(data.products); addOrders(data.orders);
      history.push({ role: "assistant", content: data.reply });
    } catch {
      typing.remove();
      addMessage(t("error"), "bot");
    } finally { sending = false; }
  }

  form.addEventListener("submit", (event) => { event.preventDefault(); send(input.value); });
  input.addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !event.shiftKey) { event.preventDefault(); send(input.value); }
  });
  root.querySelectorAll("[data-chat-prompt]").forEach(button => button.addEventListener("click", () => send(button.dataset.chatPrompt)));
  root.querySelectorAll(".ai-chat__social a").forEach(link => link.addEventListener("click", () => {
    if (conversationId) postJson(`/api/chat/handoff/${conversationId}`, {});
  }));
})();
