(function () {
  let url = location.origin;
  if (location.protocol === "file:" || location.port === "5500") url = "http://localhost:5080";
  window.GUIAFOCO_API_URL = url;
})();