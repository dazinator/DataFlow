# Theming the DataFlow Blazor Components

The `Uniun.DataFlow.Blazor` package ships with a default visual theme that can be
customised entirely through CSS custom properties (variables). No component code
changes are required — just override the variables you care about in your own
stylesheet.

---

## Setup

Add both `<link>` tags to your host application. The first loads the scoped
component styles (generated automatically by the Blazor build toolchain). The
second loads the default theme variables and is the file you target for
overrides.

```html
<!-- index.html (WASM) or App.razor (Server) -->

<!-- 1. Scoped component styles — always required -->
<link rel="stylesheet" href="_content/Uniun.DataFlow.Blazor/Uniun.DataFlow.Blazor.styles.css" />

<!-- 2. Default theme variables — required; override below this line -->
<link rel="stylesheet" href="_content/Uniun.DataFlow.Blazor/dataflow-blazor.css" />

<!-- 3. Your app stylesheet — place overrides here -->
<link rel="stylesheet" href="app.css" />
```

Load your own stylesheet **after** the library stylesheet so your overrides take
precedence.

---

## Applying overrides

Redefine any variable on `:root` in your own CSS and the change cascades into
every DataFlow component automatically.

```css
/* app.css */
:root {
    --df-panel-header-from: #1a1a2e;
    --df-panel-header-to:   #16213e;
    --df-radius:            4px;
}
```

---

## Variable reference

### Panel header

Controls the gradient bar at the top of `FlowStatusPanel`.

| Variable | Default | Description |
|---|---|---|
| `--df-panel-header-from` | `#667eea` | Gradient start colour (top-left) |
| `--df-panel-header-to` | `#764ba2` | Gradient end colour (bottom-right) |
| `--df-panel-header-text` | `#ffffff` | Text colour inside the header |

---

### Status colours

Used by status badges in `FlowStatusPanel` and by node borders / backgrounds in
`FlowDiagram`.

| Variable | Default | Description |
|---|---|---|
| `--df-status-idle` | `#6c757d` | Idle state colour (grey) |
| `--df-status-running` | `#ffc107` | Running state colour (amber) |
| `--df-status-running-text` | `#000000` | Text colour on running badges |
| `--df-status-running-bg` | `#fff9e6` | Light tint fill for running diagram nodes |
| `--df-status-completed` | `#28a745` | Completed state colour (green) |
| `--df-status-completed-bg` | `#e8f5e9` | Light tint fill for completed diagram nodes |
| `--df-status-failed` | `#dc3545` | Failed state colour (red) |
| `--df-status-failed-bg` | `#ffebee` | Light tint fill for failed diagram nodes |

---

### Surface colours

Used for card backgrounds, the diagram canvas, interactive button states, and
borders throughout all components.

| Variable | Default | Description |
|---|---|---|
| `--df-surface-bg` | `#ffffff` | Primary surface (cards, node backgrounds) |
| `--df-surface-muted` | `#f8f9fa` | Slightly off-white secondary surfaces |
| `--df-surface-hover` | `#e9ecef` | Button/toggle hover background |
| `--df-surface-border` | `#dee2e6` | Card and node borders |
| `--df-diagram-bg` | `#f8f9fa` | Background fill of the diagram canvas |

---

### Text colours

| Variable | Default | Description |
|---|---|---|
| `--df-text-body` | `#212529` | Primary readable text (JSON viewer) |
| `--df-text-muted` | `#495057` | Secondary / label text |
| `--df-text-subtle` | `#868e96` | Icon and decorative text (chevrons) |
| `--df-text-faint` | `#adb5bd` | Hints and placeholder text |
| `--df-text-loading` | `#666666` | Loading spinner message text |

---

### Shape

| Variable | Default | Description |
|---|---|---|
| `--df-radius` | `8px` | Standard border radius on cards, nodes, and the diagram panel |
| `--df-radius-badge` | `20px` | Border radius for pill-shaped status badges |

---

### Shadows

| Variable | Default | Description |
|---|---|---|
| `--df-shadow-sm` | `0 2px 4px rgba(0,0,0,0.1)` | Subtle shadow (diagram panel, nodes) |
| `--df-shadow-md` | `0 4px 6px rgba(0,0,0,0.1)` | More prominent shadow (status panel header) |

---

### Typography

| Variable | Default | Description |
|---|---|---|
| `--df-font-sans` | `'Segoe UI', Tahoma, Geneva, Verdana, sans-serif` | Base sans-serif font stack for the visualization wrapper |
| `--df-font-mono` | `'Consolas', 'Monaco', monospace` | Monospace font stack for JSON / parameter viewer |

---

## Example: dark theme

```css
/* app.css — full dark theme override */
:root {
    /* panel */
    --df-panel-header-from: #0f2027;
    --df-panel-header-to:   #203a43;

    /* surfaces */
    --df-surface-bg:        #1e1e2e;
    --df-surface-muted:     #2a2a3e;
    --df-surface-hover:     #313150;
    --df-surface-border:    #44475a;
    --df-diagram-bg:        #181825;

    /* text */
    --df-text-body:         #cdd6f4;
    --df-text-muted:        #a6adc8;
    --df-text-subtle:       #6c7086;
    --df-text-faint:        #45475a;
    --df-text-loading:      #a6adc8;

    /* shapes */
    --df-radius:            6px;
}
```

---

## What cannot be themed via variables

Component **layout and structure** (flex direction, padding values, animation
timing, font sizes) is controlled by scoped CSS that is compiled into
`Uniun.DataFlow.Blazor.styles.css`. These rules are intentionally isolated and
cannot be overridden by normal selectors from a consuming app. If you need
structural changes, raise an issue or contribute to the library.

The `node-pulse` animation keyframe shadow colour in `FlowDiagram` is currently
hardcoded to an amber tint (`rgba(255, 193, 7, …)`) because CSS variables cannot
be used inside `rgba()` keyframe colour stops in all browsers. This will be
updated when broader browser support for relative colour syntax is available.
