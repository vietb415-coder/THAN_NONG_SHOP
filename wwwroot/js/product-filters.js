(() => {
  const form = document.getElementById('product-filters');
  const results = document.getElementById('product-results');
  if (!form || !results) return;
  let pending;
  async function update(push = true) {
    clearTimeout(typing);
    if (!form.reportValidity()) return;
    const min = form.elements.minPrice;
    const max = form.elements.maxPrice;
    if (min.value !== '' && max.value !== '' && Number(min.value) > Number(max.value)) {
      [min.value, max.value] = [max.value, min.value];
    }
    pending?.abort(); pending = new AbortController();
    const url = new URL(form.action); url.search = new URLSearchParams(new FormData(form));
    results.setAttribute('aria-busy', 'true');
    const request = pending;
    try {
      const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, signal: request.signal });
      if (!response.ok) throw new Error('Không tải được danh sách');
      const html = await response.text();
      if (request !== pending) return;
      results.innerHTML = html;
      if (push) history.pushState(null, '', url);
    } catch (error) {
      if (error.name !== 'AbortError') { let alert = document.getElementById('filter-error'); if (!alert) { alert=document.createElement('p'); alert.id='filter-error'; alert.setAttribute('role','alert'); results.prepend(alert); } alert.textContent='Chưa tải được sản phẩm. Vui lòng thử lại.'; }
    } finally { if (request === pending) results.removeAttribute('aria-busy'); }
  }
  form.addEventListener('submit', event => { event.preventDefault(); update(); });
  let typing;
  form.elements.searchString.addEventListener('input', () => {
    clearTimeout(typing);
    pending?.abort();
    typing = setTimeout(() => update(), 300);
  });
  form.querySelector('a').addEventListener('click', event => {
    event.preventDefault(); clearTimeout(typing);
    for (const name of ['searchString','categoryId','minPrice','maxPrice']) form.elements[name].value = '';
    update();
  });
  form.querySelectorAll('select,input[type=number]').forEach(input => input.addEventListener('change', () => update()));
  window.addEventListener('popstate', () => { const values=new URL(location.href).searchParams; for(const name of ['searchString','categoryId','minPrice','maxPrice'])form.elements[name].value=values.get(name)||''; update(false); });
})();
