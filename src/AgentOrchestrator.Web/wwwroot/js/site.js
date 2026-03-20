// Theme toggle
(function () {
    var toggle = document.getElementById('theme-toggle');
    var iconLight = document.getElementById('theme-icon-light');
    var iconDark = document.getElementById('theme-icon-dark');

    function applyIcons() {
        var isDark = document.body.classList.contains('dark-mode');
        iconLight.style.display = isDark ? '' : 'none';
        iconDark.style.display = isDark ? 'none' : '';
    }

    applyIcons();

    toggle.addEventListener('click', function () {
        document.body.classList.toggle('dark-mode');
        var isDark = document.body.classList.contains('dark-mode');
        localStorage.setItem('theme', isDark ? 'dark' : 'light');
        applyIcons();
    });
})();
