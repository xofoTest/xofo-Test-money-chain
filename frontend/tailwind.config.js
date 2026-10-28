/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        navy: { 900: '#0A1F33', 800: '#0F2B47', 700: '#14375C' },
        brand: { 700: '#1D4ED8', 600: '#2563EB', 100: '#DBEAFE', 50: '#EFF6FF' }
      },
      borderRadius: { card: '14px' },
      boxShadow: {
        sm: '0 1px 2px rgba(15,23,42,.06), 0 1px 3px rgba(15,23,42,.08)',
        md: '0 6px 18px rgba(15,23,42,.09)'
      }
    }
  },
  plugins: []
};
