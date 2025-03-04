
function getStoredTheme(theme) {
    return localStorage.getItem('sfx-theme')
}

function setStoredTheme(theme) {
    localStorage.setItem('sfx-theme', theme)
}

function getPreferredTheme() {
    const storedTheme = getStoredTheme()
    if (storedTheme) {
        return storedTheme
    }

    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function setTheme(theme) {
    if (theme === 'auto') {
        document.documentElement.setAttribute('data-bs-theme', (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'))
    } else {
        document.documentElement.setAttribute('data-bs-theme', theme)
    }
}

export { getPreferredTheme, setTheme, setStoredTheme }