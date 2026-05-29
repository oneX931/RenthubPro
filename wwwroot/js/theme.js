window.rentHubTheme = (function () {
    function setCookie(name, value) {
        document.cookie = name + '=' + encodeURIComponent(value) + '; path=/; max-age=' + (60 * 60 * 24 * 365) + '; samesite=lax';
    }

    function apply(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        try { localStorage.setItem('theme', theme); } catch (e) { }
        setCookie('theme', theme);
        fetch('/theme/set?value=' + theme, { method: 'POST' }).catch(function () { });
    }

    function current() {
        return document.documentElement.getAttribute('data-theme') || 'light';
    }

    function readStored() {
        var m = document.cookie.match(/(?:^|; )theme=([^;]+)/);
        if (m) return decodeURIComponent(m[1]);
        try { return localStorage.getItem('theme') || 'light'; } catch (e) { return 'light'; }
    }

    function reapply() {
        document.documentElement.setAttribute('data-theme', readStored());
    }

    function toggle() {
        apply(current() === 'dark' ? 'light' : 'dark');
        return current();
    }

    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancedload', reapply);
    }

    return { apply: apply, toggle: toggle, current: current, reapply: reapply };
})();
