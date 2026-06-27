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

### ✅ Incremento 3 — Barra de herramientas + primera herramienta (rectángulo)
Barra flotante anclada a la selección con indicación de herramienta activa; primera
herramienta de dibujo (rectángulo contorno) enganchada al ciclo herramienta→comando→canvas.
- **Cubre:** RF-8 (parcial) · PRD §6 · Arquitectura §6
- **Por qué la más simple:** valida el patrón completo de extremo a extremo con bajo riesgo.
- **Estado:** Completado y validado por el usuario (pruebas en pantalla a 100% y 150% OK).
- **Notas de diseño / alcance:**
  - Nueva capa `AnnotationCanvas` entre el oscurecimiento y el cromo de selección, para que
    las anotaciones se vean a brillo pleno. `AnnotationCanvasController` (presentación)
    materializa el modelo de `Zass.Core` a `Shape` de WPF y enruta undo/redo.
  - Barra con **dos** herramientas funcionales: Puntero (editar selección) y Rectángulo
    (contorno), con indicación de herramienta activa. El resto de herramientas del PRD §6
    (texto, flecha, relleno, libre, color/grosor, copiar/guardar) llegan en Incrementos 4–6.
  - Color/grosor por defecto fijos (rojo, 3 px físicos); su UI llega en el Incremento 5.
  - Atajos: V = puntero, R = rectángulo, Ctrl+Z/Ctrl+Y = deshacer/rehacer, Ctrl+C/Enter =
    copiar, Esc = cancelar. Borrar anotación seleccionada se difiere (requiere selección de
    anotaciones con el puntero, en un incremento posterior).
  - **Composición de exportación traída adelante (parcial del Incremento 6):** la copia al
    portapapeles ahora compone fondo recortado + anotaciones dentro de la selección,
    excluyendo capas auxiliares. La fidelidad píxel a píxel formal y el guardado a disco
    siguen siendo del Incremento 6.
  - **Decisión confirmada por el usuario:** las anotaciones mantienen **posición absoluta**
    (Arquitectura §15); al mover la selección con el puntero no la acompañan.

### ✅ Incremento 4 — Resto de herramientas de dibujo + texto
Flecha, rectángulo relleno, dibujo libre (un solo trazo = un solo comando) y, al final por su
delicadeza, **texto** con gestión de foco (escritura vs. atajos de herramienta).
- **Cubre:** RF-8, RF-13 · Arquitectura §6.3
- **Riesgo:** solapamiento teclas-herramienta durante la edición de texto.
- **Estado:** Completado y validado por el usuario (compila sin avisos; 62 tests en verde).
- **Notas de diseño / alcance:**
  - El modelo de estas anotaciones ya existía desde el Incremento 1; este incremento es
    **solo capa de presentación**: barra ampliada, dibujo en vivo y materialización a WPF.
  - `AnnotationCanvasController` pasa a un **flujo de borrador unificado** (`BeginDraft` /
    `UpdateDraft` / `CommitDraft`): el objeto que se previsualiza durante el arrastre es el
    mismo que se confirma, evitando geometría duplicada. Sustituye al par
    `ShowRectanglePreview`/`CommitRectangle` del Incremento 3. El diccionario interno pasa de
    `Shape` a `FrameworkElement` para alojar también `TextBlock` (texto) y `Path` (flecha).
  - Materialización: rectángulo contorno y relleno → `Rectangle`; flecha → `Path` (asta + dos
    barbas, cabeza proporcional al grosor); dibujo libre → `Polyline` (un punto cada ≥1,5 px
    físicos); texto → `TextBlock`.
  - **Dibujo libre = un solo comando:** el trazo entero es un único `FreehandAnnotation` y un
    único `AddAnnotationCommand` (Arquitectura §7.2). Un Ctrl+Z deshace el trazo completo.
  - **Texto con gestión de foco (Arquitectura §6.3, RF-13):** clic para colocar un `TextBox`
    en línea; mientras tiene el foco, la ventana ignora todas las teclas (las alfanuméricas
    escriben, no activan herramientas). **Enter** o **Esc** confirman el texto como objeto
    (Esc no cierra el overlay mientras se escribe); un clic fuera también confirma. Texto vacío
    se descarta. Al confirmar, el foco de teclado vuelve a la ventana.
  - **Limitación conocida:** durante la edición de texto, los atajos con modificador
    (Ctrl+Z/Y/C/S) los gestiona el `TextBox` (su propio undo/copiar), no la ventana. La
    operatividad de Ctrl+… a nivel de ventana durante la edición (Arquitectura §6.3) se puede
    afinar más adelante; el comportamiento actual es seguro y no rompe la escritura.
  - **Atajos añadidos:** T = texto, A = flecha, F = rectángulo relleno, D = dibujo libre
    (V = puntero y R = rectángulo ya existían).
  - **Color/grosor/tamaño** siguen fijos por defecto (rojo, 3 px de trazo, 18 px de texto);
    su UI llega en el Incremento 5. La **selección/movimiento/borrado de anotaciones** con el
    puntero (RF-11 en presentación) sigue diferida a un incremento posterior.

### ✅ Incremento 5 — Color y grosor
Paleta rápida + selector de color completo; grosor de trazo y tamaño de texto. Persistencia
del último valor dentro de la sesión de overlay.
- **Cubre:** RF-9, RF-10
- **Estado:** Completado y validado por el usuario (compila sin avisos; 62 tests en verde).
- **Decisiones confirmadas por el usuario:**
  - **Selector de color completo = popup WPF propio** (no `ColorDialog` de WinForms): evita
    añadir WinForms y un diálogo nativo que quedaría detrás del overlay topmost. Control nuevo
    `ColorPicker` (UserControl): paleta rápida de 12 colores + área saturación/brillo + barra de
    tono + campo hex. HSV↔RGB propios, cero dependencias nuevas.
  - **Grosor y tamaño = sliders de rango completo** (1–20 px de trazo; 8–72 de texto), fieles
    al PRD, con el valor numérico visible.
- **Notas de diseño / alcance:**
  - Los controles de opciones se muestran **según la herramienta activa**: color para todas las
    de dibujo y texto; grosor para flecha/rectángulo/libre (el relleno no tiene trazo); tamaño
    solo para texto. La barra se reancla al cambiar de ancho.
  - **Persistencia dentro de la sesión:** `_currentColor`/`_currentThickness`/`_currentTextSize`
    son campos de la ventana; el último valor se mantiene entre anotaciones hasta cerrar el
    overlay. La persistencia **entre sesiones** (a `settings.json`) es del Incremento 7.
  - **Foco de teclado:** el campo hex es un `TextBox`; mientras tiene el foco la ventana ignora
    los atajos de una tecla (misma regla que el texto en línea del Incremento 4). Esc cierra
    primero el popup de color; si no hay popup, cancela la captura.
  - El cambio de color/grosor afecta solo a las **nuevas** anotaciones (no hay anotación
    seleccionada todavía; la selección/edición de anotaciones con el puntero sigue diferida).
  - El botón de color es un toggle "manual": un flag de una sola pasada evita que el clic que
    cierra el popup lo reabra o inicie un dibujo por debajo.

### ✅ Incremento 6 — Exportación completa
Composición con `RenderTargetBitmap` **excluyendo capas auxiliares** (oscurecimiento,
tiradores, marcos), recorte a la selección, fidelidad píxel a píxel. Guardar a disco con
`SaveFileDialog`, PNG/JPG y nombre por defecto con marca temporal.
- **Cubre:** RF-14, RF-15, RF-16, RF-17 · Arquitectura §8
- **Estado:** Completado y validado por el usuario (compila sin avisos; 71 tests en verde).
- **Nota:** el copiar al portapapeles ya existe desde el Incremento 0; aquí se asegura la
  fidelidad y se añade el guardado a disco.
- **Notas de diseño / alcance:**
  - **Guardado a disco (RF-15/16/17):** botón Guardar y atajo **Ctrl+S** abren
    `Microsoft.Win32.SaveFileDialog` (WPF, sin WinForms) con filtros PNG/JPG y nombre por
    defecto `Zass_yyyy-MM-dd_HHmmss.png`. El overlay baja `Topmost` mientras el diálogo
    modal está abierto para que no quede oculto tras la pantalla congelada, y lo restaura si
    el usuario cancela. Solo se cierra el overlay tras una escritura correcta; un error de
    escritura se notifica por la UI y mantiene el overlay abierto.
  - **Composición reutilizada:** copiar y guardar comparten `ComposeForExport()` (fondo
    recortado + anotaciones dentro de la selección, capas auxiliares excluidas por diseño).
  - **Naming UI-agnóstico testeable:** `Zass.Core.Export.ExportNaming` (nombre por defecto +
    formato según extensión) vive en `Zass.Core` sin WPF; la codificación PNG/JPG
    (`PngBitmapEncoder`/`JpegBitmapEncoder`) vive en `Zass.App.Imaging.ImageExporter` para
    mantener `Zass.Core` libre de WPF (misma decisión que en el Incremento 1).
  - **Barra completada (PRD §6):** se añadieron los botones **Copiar**, **Guardar** y
    **Cancelar**, siempre visibles, además de los atajos ya existentes (Ctrl+C/Enter, Esc).
  - **Calidad JPG** fija en 90 por ahora; su configuración en Ajustes llega en el Incremento 7.
  - **Gap detectado fuera de alcance:** RF-5 (seleccionar el monitor completo sin arrastrar)
    no está asignado a ningún incremento del plan; queda pendiente de ubicar (no es de este).

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
