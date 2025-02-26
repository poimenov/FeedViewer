function SetOpenLinkProvider(provider) {
  window.openLinkProvider = provider;
}

function OpenLink() {
  if (window.openLinkProvider != null && event.target.tagName == "A" && event.target.href != "#") {
    event.preventDefault();
    window.openLinkProvider.invokeMethodAsync('OpenLink', event.target.href);
  }
}