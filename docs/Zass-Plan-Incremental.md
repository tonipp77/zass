# Plan de Ejecución Incremental — Zass

**Versión:** 1.0
**Estado:** Vivo (se actualiza al completar cada incremento)
**Documentos relacionados:** `Zass-PRD.md`, `Zass-Arquitectura-Tecnica.md`

---

## Propósito

Este documento es el **mapa de actuación** del desarrollo de Zass v1. Define el orden en que
se construyen las funcionalidades, en incrementos pequeños y compilables, alineado con la regla
del proyecto: *no generar el proyecto entero de golpe*. Cada sesión de trabajo debe consultar
este plan, retomar por el primer incremento no completado y actualizar su estado al terminar.

Cómo referenciarlo en otras sesiones: *"continúa por el incremento N del plan incremental"*.

---

## Principio de validación

Gran parte de Zass (DPI, nitidez, portapapeles, foco de teclado) **solo puede validarla el
usuario manualmente**, porque el desarrollador IA no ve la pantalla. Por eso:

- Los incrementos se entregan **de uno en uno**, compilando antes de continuar.
- Cada incremento con efecto visible termina con una **lista de verificación manual** para el usuario.
- No se apila el siguiente incremento hasta que el usuario valida el anterior.
- **No se hace commit ni se sube nada a `dev` hasta que el usuario confirme** que el incremento
  pasa su plan de pruebas. Hasta esa confirmación el código permanece en el árbol de trabajo
  (sin commit). Un incremento solo pasa a ✅ tras la validación del usuario; antes está 🔄.

---

## Estado de incrementos

Leyenda: ⬜ pendiente · 🔄 en curso · ✅ completado y validado

### ✅ Incremento 0 — Esqueleto vertical
Flujo mínimo de extremo a extremo: atajo global → captura del monitor activo (GDI BitBlt,
píxeles físicos) → overlay congelado → selección → copia al portapapeles. Interop Win32
aislada, manifiesto Per-Monitor DPI Aware v2, tests de `SelectionGeometry`.
**Estado:** Completado y validado por el usuario.

### ✅ Incremento 1 — Núcleo de anotaciones + undo/redo (`Zass.Core`)
Modelo de objetos vectoriales (`Annotation` y subclases), `IUndoableCommand`,
`UndoRedoManager` (dos pilas), comandos `Add/Remove/Move`. **Sin UI, con tests unitarios.**
- **Cubre:** RF-11, RF-12 · Arquitectura §6, §7
- **Por qué primero:** es el corazón del diferenciador, es C# puro testeable sin verificación
  visual, y desbloquea todo lo demás.
- **Estado:** Completado y validado por el usuario (28 tests unitarios en verde).
- **Nota de diseño:** `Render()` (Arquitectura §6.1) se omite deliberadamente del modelo
  para mantener `Zass.Core` libre de WPF (restricción de CLAUDE.md). La materialización a
  `UIElement` vivirá en la capa de presentación (`Zass.App`) en el Incremento 3. El color se
  modela con `ArgbColor` (UI-agnóstico) y los puntos con `PhysicalPoint` (píxeles físicos).

### ✅ Incremento 2 — Capa de selección completa en el overlay
Redimensionado y reposicionado de la selección con 8 tiradores, capa de oscurecimiento
("agujero"), indicador de dimensiones en píxeles durante dibujo/redimensionado.
- **Cubre:** RF-4, RF-6 · PRD §6 · Arquitectura §5.2
- **Estado:** Completado y validado por el usuario (pruebas en pantalla a 100% y 150% OK).
- **Notas de diseño:**
  - Lógica de manipulación extraída a `Zass.Core.Capture.SelectionManipulator` (puro,
    píxeles físicos, 22 tests): hit-test de los 8 tiradores, `Resize` (borde opuesto fijo,
    clamp a monitor, tamaño mínimo, **sin volteo** en v1) y `Move` (clamp de posición).
  - Cambio de flujo respecto al Incremento 0: al soltar el ratón la selección **ya no se
    copia automáticamente**; queda editable (tiradores/mover) y se confirma con **Enter**.
    Esc cancela. Esto da margen al futuro toolbar de anotaciones (Incrementos 3+).

### ⬜ Incremento 3 — Barra de herramientas + primera herramienta (rectángulo)
Barra flotante anclada a la selección con indicación de herramienta activa; primera
herramienta de dibujo (rectángulo contorno) enganchada al ciclo herramienta→comando→canvas.
- **Cubre:** RF-8 (parcial) · PRD §6 · Arquitectura §6
- **Por qué la más simple:** valida el patrón completo de extremo a extremo con bajo riesgo.

### ⬜ Incremento 4 — Resto de herramientas de dibujo + texto
Flecha, rectángulo relleno, dibujo libre (un solo trazo = un solo comando) y, al final por su
delicadeza, **texto** con gestión de foco (escritura vs. atajos de herramienta).
- **Cubre:** RF-8, RF-13 · Arquitectura §6.3
- **Riesgo:** solapamiento teclas-herramienta durante la edición de texto.

### ⬜ Incremento 5 — Color y grosor
Paleta rápida + selector de color completo; grosor de trazo y tamaño de texto. Persistencia
del último valor dentro de la sesión de overlay.
- **Cubre:** RF-9, RF-10

### ⬜ Incremento 6 — Exportación completa
Composición con `RenderTargetBitmap` **excluyendo capas auxiliares** (oscurecimiento,
tiradores, marcos), recorte a la selección, fidelidad píxel a píxel. Guardar a disco con
`SaveFileDialog`, PNG/JPG y nombre por defecto con marca temporal.
- **Cubre:** RF-14, RF-15, RF-16, RF-17 · Arquitectura §8
- **Nota:** el copiar al portapapeles ya existe desde el Incremento 0; aquí se asegura la
  fidelidad y se añade el guardado a disco.

### ⬜ Incremento 7 — Producto: bandeja, ajustes, i18n y persistencia
Menú de bandeja completo (Capturar, Ajustes, Acerca de, Salir), pantalla de Ajustes,
localización es/en conmutable en caliente (`ResourceDictionary`), persistencia JSON en
`%APPDATA%\Zass\settings.json`, arranque con Windows (clave Run HKCU).
- **Cubre:** RF-18, RF-19, RF-20, RF-21 · Arquitectura §9, §10

### ⬜ Incremento 8 — Empaquetado y distribución
Instalador Inno Setup y/o build portable self-contained. Firma de código diferida a v2.
- **Cubre:** Arquitectura §13

---

## Hito v1

La v1 se considera completa cuando los Incrementos 1–7 estén validados contra los
**criterios de aceptación globales del PRD (§10)**. El Incremento 8 es distribución.

## Fuera de alcance v1

Multimonitor, captura de ventana/retardo, nube/compartir, selector de fuente, autoactualización,
firma de código, vídeo/GIF y edición avanzada. Ver PRD §12 (roadmap v2+).
