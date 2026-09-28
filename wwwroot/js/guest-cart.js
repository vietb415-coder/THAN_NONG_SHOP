(async () => {
  const key='thannong.guest-cart.v1';
  try {
    const response=await fetch('/Cart/Snapshot', {cache:'no-store'}); if(!response.ok)return;
    let state=await response.json();
    if(state.authenticated) {localStorage.removeItem(key);return;}
    const token=document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    if(!state.initialized && token) {
      let saved;try{saved=JSON.parse(localStorage.getItem(key)||'[]');}catch{saved=[];}
      if(Array.isArray(saved) && saved.length>0 && saved.length<=30){
        const restored=await fetch('/Cart/RestoreGuest',{method:'POST',headers:{'Content-Type':'application/json','RequestVerificationToken':token},body:JSON.stringify(saved)});
        if(restored.ok){const current=await fetch('/Cart/Snapshot',{cache:'no-store'});if(current.ok)state=await current.json();if(location.pathname.toLowerCase().startsWith('/cart') && state.items.length) location.reload();}
      }
    }
    localStorage.setItem(key,JSON.stringify(state.items));
  } catch { /* Cookies / server cart remain usable when storage is disabled. */ }
})();
