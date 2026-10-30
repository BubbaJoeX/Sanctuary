// A right-click menu over the view: a list of actions at the pointer, closed by picking one, Escape, a click or a
// scroll elsewhere. Items are { label, key (its shortcut, shown), action, disabled, danger, title }, a heading
// ({ heading }) or a line ("-").

let open = null;

export function closeMenu() {
  if (!open) return;
  open.remove();
  open = null;
  window.removeEventListener("pointerdown", onOutside, true);
  window.removeEventListener("keydown", onKey, true);
  window.removeEventListener("wheel", closeMenu, true);
  window.removeEventListener("blur", closeMenu);
}

function onOutside(event) {
  if (open && !open.contains(event.target)) closeMenu();
}

function onKey(event) {
  if (event.key !== "Escape") return;
  event.stopPropagation();
  closeMenu();
}

export function showMenu(x, y, items) {
  closeMenu();
  const menu = document.createElement("div");
  menu.className = "context-menu";
  menu.setAttribute("role", "menu");
  for (const item of items) {
    if (item === "-") {
      menu.appendChild(document.createElement("hr"));
      continue;
    }
    if (item.heading) {
      const heading = document.createElement("div");
      heading.className = "context-heading";
      heading.textContent = item.heading;
      menu.appendChild(heading);
      continue;
    }
    const button = document.createElement("button");
    button.type = "button";
    button.setAttribute("role", "menuitem");
    button.disabled = !!item.disabled;
    if (item.danger) button.classList.add("danger");
    if (item.title) button.title = item.title;
    button.innerHTML = `<span></span>${item.key ? `<kbd>${item.key}</kbd>` : ""}`;
    button.firstChild.textContent = item.label;
    button.addEventListener("click", () => {
      closeMenu();
      item.action();
    });
    menu.appendChild(button);
  }
  document.body.appendChild(menu);
  // Kept on screen: it opens up or left of the pointer when there's no room below or right of it.
  const { width, height } = menu.getBoundingClientRect();
  menu.style.left = `${Math.max(4, x + width > innerWidth - 4 ? x - width : x)}px`;
  menu.style.top = `${Math.max(4, y + height > innerHeight - 4 ? y - height : y)}px`;
  open = menu;
  window.addEventListener("pointerdown", onOutside, true);
  window.addEventListener("keydown", onKey, true);
  window.addEventListener("wheel", closeMenu, true);
  window.addEventListener("blur", closeMenu);
  menu.querySelector("button:not(:disabled)")?.focus({ preventScroll: true });
}
