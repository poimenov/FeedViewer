function AddKeydownHandler() {
  document.addEventListener('keydown', (event) => {
    if (event.key === 'ArrowUp' || event.key === 'ArrowLeft') {
      ClickFlipper("previous");
    }
    if (event.key === 'ArrowDown' || event.key === 'ArrowRight') {
      ClickFlipper("next");
    }
  });
}

function ClickFlipper(direction) {
  var contentCard = document.getElementById("contentCard");
  if(contentCard == null){
    return;
  }
  var flippers = contentCard.getElementsByTagName("fluent-flipper");
  for (var i = 0; i < flippers.length; i++) {
    var flipper = flippers[i];
    if (flipper.direction == direction && flipper.disabled != true) {
      flipper.click();
      break;
    }
  }
}

function SetOpenLinkProvider(provider) {
  window.openLinkProvider = provider;
  AddKeydownHandler();
}

function OpenLink() {
  if (window.openLinkProvider != null && event.target.tagName == "A" && event.target.href != "#") {
    event.preventDefault();
    window.openLinkProvider.invokeMethodAsync('OpenLink', event.target.href);
  }
}

function ScrollToSelectedItem() {
  var selected = document.getElementsByClassName('selected-channel-item');
  if(selected.length > 0) {
      selected[0].scrollIntoView({ block: "center", behavior: "smooth" });
  }
}