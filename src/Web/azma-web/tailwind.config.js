import animate from 'tailwindcss-animate';

/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    container: {
      center: true,
      padding: '2rem',
      screens: { '2xl': '1400px' },
    },
    extend: {
      fontFamily: {
        sans: ['Vazirmatn', 'IRANSansX', 'IRANSans', 'Tahoma', 'Segoe UI', 'system-ui', 'sans-serif'],
        display: ['Vazirmatn', 'IRANSansX', 'IRANSans', 'Tahoma', 'Segoe UI', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'SFMono-Regular', 'monospace'],
      },
      fontSize: {
        '2xs': ['0.625rem', { lineHeight: '1rem' }],
        xs:    ['0.75rem',  { lineHeight: '1.125rem' }],
        sm:    ['0.8125rem',{ lineHeight: '1.25rem' }],
        base:  ['0.9375rem',{ lineHeight: '1.5rem' }],
        lg:    ['1.0625rem',{ lineHeight: '1.625rem' }],
        xl:    ['1.1875rem',{ lineHeight: '1.75rem' }],
        '2xl': ['1.375rem', { lineHeight: '1.875rem' }],
        '3xl': ['1.625rem', { lineHeight: '2.125rem' }],
      },
      letterSpacing: {
        tighter: '-0.04em',
        tight:   '-0.02em',
        normal:  '0em',
        wide:    '0.02em',
        wider:   '0.05em',
        widest:  '0.1em',
      },
      colors: {
        border: 'hsl(var(--border))',
        input: 'hsl(var(--input))',
        ring: 'hsl(var(--ring))',
        background: 'hsl(var(--background))',
        foreground: 'hsl(var(--foreground))',
        primary: {
          DEFAULT: 'hsl(var(--primary))',
          foreground: 'hsl(var(--primary-foreground))',
        },
        secondary: {
          DEFAULT: 'hsl(var(--secondary))',
          foreground: 'hsl(var(--secondary-foreground))',
        },
        destructive: {
          DEFAULT: 'hsl(var(--destructive))',
          foreground: 'hsl(var(--destructive-foreground))',
        },
        success: {
          DEFAULT: 'hsl(var(--success))',
          foreground: 'hsl(var(--success-foreground))',
        },
        warning: {
          DEFAULT: 'hsl(var(--warning))',
          foreground: 'hsl(var(--warning-foreground))',
        },
        info: {
          DEFAULT: 'hsl(var(--info))',
          foreground: 'hsl(var(--info-foreground))',
        },
        muted: {
          DEFAULT: 'hsl(var(--muted))',
          foreground: 'hsl(var(--muted-foreground))',
        },
        accent: {
          DEFAULT: 'hsl(var(--accent))',
          foreground: 'hsl(var(--accent-foreground))',
        },
        popover: {
          DEFAULT: 'hsl(var(--popover))',
          foreground: 'hsl(var(--popover-foreground))',
        },
        card: {
          DEFAULT: 'hsl(var(--card))',
          foreground: 'hsl(var(--card-foreground))',
        },
        sidebar: {
          DEFAULT: 'hsl(var(--sidebar))',
          foreground: 'hsl(var(--sidebar-foreground))',
          accent: 'hsl(var(--sidebar-accent))',
          'accent-foreground': 'hsl(var(--sidebar-accent-foreground))',
          border: 'hsl(var(--sidebar-border))',
        },
      },
      borderRadius: {
        lg: 'var(--radius)',
        md: 'calc(var(--radius) - 2px)',
        sm: 'calc(var(--radius) - 4px)',
      },
      boxShadow: {
        soft:      '0 1px 2px 0 hsl(220 16% 4% / 0.04), 0 1px 3px 0 hsl(220 16% 4% / 0.03)',
        'soft-md': '0 4px 8px -2px hsl(220 16% 4% / 0.08), 0 2px 4px -2px hsl(220 16% 4% / 0.04)',
        'soft-lg': '0 12px 32px -8px hsl(220 16% 4% / 0.18), 0 4px 10px -4px hsl(220 16% 4% / 0.06)',
        'soft-xl': '0 24px 64px -12px hsl(220 16% 4% / 0.25), 0 8px 16px -6px hsl(220 16% 4% / 0.08)',
        'primary': '0 0 0 3px hsl(var(--primary) / 0.18)',
      },
      transitionTimingFunction: {
        'ease-smooth': 'cubic-bezier(0.25, 0, 0, 1)',
        'bounce-in':   'cubic-bezier(0.34, 1.56, 0.64, 1)',
      },
      transitionDuration: {
        '50':  '50ms',
        '150': '150ms',
        '250': '250ms',
        '350': '350ms',
      },
      keyframes: {
        'accordion-down': {
          from: { height: '0' },
          to: { height: 'var(--radix-accordion-content-height)' },
        },
        'accordion-up': {
          from: { height: 'var(--radix-accordion-content-height)' },
          to: { height: '0' },
        },
        shimmer: {
          '0%': { backgroundPosition: '200% 0' },
          '100%': { backgroundPosition: '-200% 0' },
        },
        'gradient-shift': {
          '0%, 100%': { backgroundPosition: '0% 50%' },
          '50%': { backgroundPosition: '100% 50%' },
        },
      },
      animation: {
        'accordion-down': 'accordion-down 0.2s ease-out',
        'accordion-up': 'accordion-up 0.2s ease-out',
        shimmer: 'shimmer 2s linear infinite',
        'gradient-shift': 'gradient-shift 8s ease infinite',
      },
    },
  },
  plugins: [animate],
};
