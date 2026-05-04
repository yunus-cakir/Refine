---
name: Dark Cyber-Academia
colors:
  surface: '#121414'
  surface-dim: '#121414'
  surface-bright: '#383939'
  surface-container-lowest: '#0d0e0f'
  surface-container-low: '#1a1c1c'
  surface-container: '#1e2020'
  surface-container-high: '#292a2a'
  surface-container-highest: '#343535'
  on-surface: '#e3e2e2'
  on-surface-variant: '#c4c9ac'
  inverse-surface: '#e3e2e2'
  inverse-on-surface: '#2f3131'
  outline: '#8e9379'
  outline-variant: '#444933'
  surface-tint: '#abd600'
  primary: '#ffffff'
  on-primary: '#283500'
  primary-container: '#c3f400'
  on-primary-container: '#556d00'
  inverse-primary: '#506600'
  secondary: '#c8c6c8'
  on-secondary: '#303032'
  secondary-container: '#474649'
  on-secondary-container: '#b6b4b7'
  tertiary: '#ffffff'
  on-tertiary: '#303030'
  tertiary-container: '#e2e2e2'
  on-tertiary-container: '#646464'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#c3f400'
  primary-fixed-dim: '#abd600'
  on-primary-fixed: '#161e00'
  on-primary-fixed-variant: '#3c4d00'
  secondary-fixed: '#e4e2e4'
  secondary-fixed-dim: '#c8c6c8'
  on-secondary-fixed: '#1b1b1d'
  on-secondary-fixed-variant: '#474649'
  tertiary-fixed: '#e2e2e2'
  tertiary-fixed-dim: '#c6c6c6'
  on-tertiary-fixed: '#1b1b1b'
  on-tertiary-fixed-variant: '#474747'
  background: '#121414'
  on-background: '#e3e2e2'
  surface-variant: '#343535'
typography:
  display-lg:
    fontFamily: Space Grotesk
    fontSize: 48px
    fontWeight: '700'
    lineHeight: '1.1'
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Space Grotesk
    fontSize: 32px
    fontWeight: '600'
    lineHeight: '1.2'
    letterSpacing: -0.01em
  title-sm:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: '1.4'
  body-md:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: '1.6'
  label-caps:
    fontFamily: Lexend
    fontSize: 12px
    fontWeight: '700'
    lineHeight: '1'
    letterSpacing: 0.1em
  stat-lg:
    fontFamily: Space Grotesk
    fontSize: 40px
    fontWeight: '700'
    lineHeight: '1'
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  unit: 4px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 40px
  gutter: 16px
  margin: 20px
---

## Brand & Style
This design system establishes a high-performance environment that blends the intellectual rigor of Dark Academia with the high-octane energy of Cyber-fitness. It targets the "elite scholar-athlete"—users who treat physical training as both a science and a discipline. 

The aesthetic is a sophisticated fusion of **Minimalism** and **High-Contrast Cyberpunk**. It utilizes a "Vantablack" depth to eliminate distractions, punctuated by surgical strikes of Neon Lime that guide the user's focus toward action and achievement. The mood is focused, premium, and intense, evoking the feeling of a private, high-tech underground laboratory dedicated to human optimization.

## Colors
The palette is built on a foundation of extreme darkness to ensure the primary accent achieves maximum luminosity. 

- **Primary (Neon Lime):** Reserved for core actions, progress indicators, and "active" states. It represents energy and the "flow state."
- **Surface (Dark Grey):** Used for structural elements and cards to provide a subtle lift from the absolute black background.
- **Background (Deep Black):** The canvas of the application, designed to recede and minimize eye strain during intense sessions.
- **Translucent White:** Used for delicate structural borders to maintain a sense of glass-like precision without adding visual weight.

## Typography
The typography strategy prioritizes readability and technical precision. 

- **Space Grotesk** is used for headlines and data visualizations (stats). Its geometric, slightly "glitchy" technical character reinforces the cyber-fitness narrative.
- **Inter** handles the heavy lifting for body copy and descriptions, providing a neutral, highly legible modern sans-serif experience.
- **Lexend** is employed for labels and navigation elements, bringing an "athletic" and readable quality to the smallest text elements.

For data-heavy screens, use tabular numbers to ensure alignment in workout logs and timers.

## Layout & Spacing
The layout follows a strict **4-pixel grid system** to maintain mathematical alignment. On mobile devices, use a fluid 4-column grid with 20px outer margins and 16px gutters. 

Elements should feel intentionally placed with generous vertical breathing room (utilizing the `xl` spacing unit) to prevent the UI from feeling cluttered. Alignment should be primarily left-justified to mimic technical documentation or scholarly journals, reinforcing the Dark Academia influence.

## Elevation & Depth
Depth is communicated through **Glassmorphism** and **Tonal Layering** rather than traditional heavy drop shadows. 

1. **Base Layer:** The absolute background (#0d0d0d).
2. **Card Layer:** Raised by 1px translucent borders and a soft, low-opacity shadow to create a subtle separation.
3. **Interactive Layer:** Buttons and active form fields utilize a "Neon Glow"—an outer shadow using the primary accent color with a high blur radius (15-20px) and low opacity (0.3) to simulate light emission.
4. **Navigation Layer:** The bottom navigation bar uses a backdrop-blur (12px to 20px) with a semi-transparent dark fill to allow background content to ghost through as the user scrolls.

## Shapes
The shape language balances "soft-tech" with "high-performance" ergonomics. 

- **Cards:** Use a 16px radius to feel premium and approachable. 
- **Form Elements:** Use a slightly tighter 12px radius to imply precision and structure.
- **Interactive Pills/Buttons:** Fully rounded (pill-shaped) to represent motion and fluidity. 

All containers should maintain a 1px solid border at 8% white opacity to define their boundaries against the dark background.

## Components

### Buttons
- **Primary:** Neon Lime background, black bold text. On hover or active state, apply a 15px Neon Lime outer glow.
- **Secondary:** Transparent background, 1px white (8% opacity) border, white text.

### Form Elements
- **Inputs:** Absolute black background, 1px border. On focus, the border transitions to Neon Lime, accompanied by a subtle inner glow. Text is primary white.
- **Checkboxes:** When checked, the box fills with Neon Lime with a black checkmark.

### Cards (.refine-card)
- Background: #1c1c1e.
- Border: 1px rgba(255, 255, 255, 0.08).
- Box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2).
- Padding: 20px.

### Badges & Pills
- Translucent background: `rgba(204, 255, 0, 0.15)`.
- Text: Neon Lime, Lexend Bold, uppercase.
- Padding: 4px 12px.

### Navigation
- **Mobile Nav:** Sticky to the bottom, 100% width. Use a frosted glass effect (`backdrop-filter: blur(16px)`) with a dark translucent fill. Icons should be thin-stroke (1.5pt) and glow Neon Lime when active.

### Data Visualization
- Line charts should use a Neon Lime stroke with a gradient "scan-line" fill underneath.
- Progress rings should use a thick Neon Lime stroke for the "completed" portion and the translucent border color for the "remaining" track.