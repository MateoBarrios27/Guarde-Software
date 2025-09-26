/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
    fontFamily: {
      sans: ['Inter', 'sans-serif'] // <-- La línea clave
      },
    },
  },
  plugins: [],
}