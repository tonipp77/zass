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

### ✅ Incremento 7 — Producto: bandeja, ajustes, i18n y persistencia
Menú de bandeja completo (Capturar, Ajustes, Acerca de, Salir), pantalla de Ajustes,
localización es/en conmutable en caliente, persistencia JSON en
`%APPDATA%\Zass\settings.json`, arranque con Windows (clave Run HKCU).
- **Cubre:** RF-18, RF-19, RF-20, RF-21 · Arquitectura §9, §10
- **Estado:** Completado y validado por el usuario (compila sin avisos; 96 tests en verde).
- **Decisiones confirmadas por el usuario:**
  - **Atajo configurable.** En el Incremento 7 el atajo se entregó fijo en Ctrl+Shift+S
    (solo lectura) por decisión del usuario; la reconfiguración (**RF-2**) se implementó
    inmediatamente después como cierre de gap (ver *Gaps cerrados* más abajo). El descriptor
    del atajo se persiste en `settings.json`.
- **Notas de diseño / alcance:**
  - **Persistencia (`Zass.Core.Settings`, sin WPF, testeable):** `AppSettings` (idioma,
    formato por defecto, calidad JPG, arranque con Windows, atajo, último color/grosor/
    tamaño) + `SettingsStore` (carga/guarda JSON con `System.Text.Json` en
    `%APPDATA%\Zass\settings.json`). `Normalize()` recorta valores fuera de rango y
    resetea enums/atajo inválidos, de modo que un archivo corrupto o editado a mano nunca
    introduce valores inválidos; un archivo ausente o ilegible cae a valores por defecto
    sin romper el arranque. El color se serializa como entero empaquetado 0xAARRGGBB.
  - **i18n conmutable en caliente:** se mantiene el enfoque de satélites `.resx` del
    Incremento 0 (en lugar de los `ResourceDictionary` que **recomienda** la Arquitectura
    §9). `LocalizationManager` cambia `CurrentUICulture` y emite `LanguageChanged`; la
    bandeja y la ventana de Ajustes refrescan sus textos en vivo y el overlay (que se
    recrea por captura) toma el idioma actual. Esto cumple RF-20 (sin reiniciar) sin
    reescribir la capa de localización ya validada. El idioma "Sistema" se resuelve contra
    la cultura del SO capturada al arrancar (es→es, resto→inglés neutro).
  - **Ventana de Ajustes (code-behind, como el resto de la UI):** idioma (Sistema/English/
    Español, con **previsualización en vivo** y revertido al cancelar), formato por defecto
    (PNG/JPG), calidad JPG (slider 1–100), iniciar con Windows, y el atajo en solo lectura.
    Los valores se persisten **solo al pulsar Guardar**; un error de escritura se notifica
    por la UI y mantiene la ventana abierta.
  - **Arranque con Windows (RF-19):** `WindowsStartup` gestiona el valor `Zass` en
    `HKCU\…\Run` (sin permisos de administrador). En cada arranque se reconcilia la clave
    con la preferencia persistida (reescribe la ruta del ejecutable si la app se movió).
  - **Acerca de:** ventana modal sencilla con nombre, versión (leída del ensamblado) y
    descripción, localizada.
  - **Menú de bandeja completo (RF-18):** Capturar ahora · Ajustes · Acerca de · Salir,
    reconstruido al cambiar de idioma.
  - **Último color/grosor/tamaño entre sesiones (RF-21):** el overlay se siembra desde los
    ajustes y, al cerrarse, devuelve los últimos valores usados, que se guardan en disco.
  - **Calidad JPG configurable:** `ImageExporter.Save` recibe la calidad desde los ajustes
    (antes fija en 90); el formato por defecto preselecciona el filtro del diálogo Guardar.

### ✅ Gaps cerrados — RF-2 (atajo configurable) y RF-5 (captura de pantalla completa)
Dos requisitos del PRD que el plan no había asignado a ningún incremento, implementados
tras validar el Incremento 7.
- **Cubre:** RF-2, RF-5 · PRD §4.1 · Arquitectura §3
- **Estado:** Completado y validado por el usuario (compila sin avisos; 120 tests en verde).
- **Notas de diseño / alcance:**
  - **RF-2 — Atajo reconfigurable sin reiniciar:** modelo `Hotkey` (modificadores + tecla)
    en `Zass.Core.Hotkeys`, UI-agnóstico y testeable: formato/parseo canónico
    (`Ctrl+Shift+S`), validación (tecla conocida + exige modificador salvo F1–F24/Impr Pant)
    y tabla bidireccional VK↔nombre (`HotkeyKeys`). `HotkeyManager` pasa de registro fijo a
    `TryApply(Hotkey)`: desregistra el anterior, registra el nuevo y, si la combinación está
    ocupada o es inválida, **restaura el anterior** y devuelve `false` (Arquitectura §3.2).
    En Ajustes, un campo "captura" graba la combinación pulsada (`KeyInterop` → VK), con
    botón **Restablecer**; al Guardar se aplica en caliente y solo entonces se persiste. Una
    combinación en uso se avisa por la UI y mantiene el atajo previo. El descriptor se
    guarda en `settings.json` y se aplica al arrancar (con *fallback* al de por defecto si
    el guardado está corrupto).
  - **RF-5 — Capturar el monitor completo sin arrastrar:** atajo **Ctrl+A** dentro del
    overlay selecciona todo el monitor activo (en píxeles físicos) dejando la selección
    editable como cualquier otra. Se añadió una **pista** discreta antes de seleccionar
    ("Arrastra para seleccionar · Ctrl+A: monitor completo · Esc: cancelar"), no interactiva
    para no interceptar clics. Nota: este Ctrl+A es un atajo **interno del overlay**,
    independiente del atajo global de captura (RF-1/RF-2).

### ⬜ Incremento 8 — Empaquetado y distribución
Instalador Inno Setup y/o build portable self-contained. Firma de código diferida a v2.
- **Cubre:** Arquitectura §13

---

## Hito v1

### 🔄 Ampliación feature-1 — Línea y collage libre

- **Solicitud:** Toni, 2026-09-08. Se desarrolla en `feature-1`, conservando el trabajo
  local previo de empaquetado. Esta petición es independiente de validar el Incremento 8.
- **Implementado:** línea (L), color/grosor e historial; añadir selección anotada al
  collage; lienzo libre con zoom, quitar/vaciar, undo/redo; nuevas capturas, recuperación
  desde bandeja y finalización mediante copia o PNG/JPG.
- **Decisiones confirmadas:** disposición libre y conservación en memoria durante la sesión.
- **Ampliación posterior solicitada:** tres flechas de collage (curva, recta, acodada),
  texto, color/tamaño e historial compartido con imágenes; herramienta de pixelado (P)
  de recortes con bloques configurables e historial de objeto completo.
- **Verificación automática:** compilación sin errores ni avisos; 130 tests unitarios
  correctos. Verificación adicional WPF de construcción del editor y composición de
  píxeles con DPI distintos, espacios blancos, solapamiento y exportación tras undo.
  Comprobaciones WPF adicionales de extremos de flechas en varias direcciones, texto,
  pixelado a 150% con origen de monitor desplazado y resultado idéntico al rehacer.
- **Estado:** implementado; commit y push a `feature-1` autorizados expresamente por Toni
  el 2026-09-09. La validación manual no se ha registrado todavía.
- **Plan de pruebas:** `Zass-Feature-1-Pruebas.md`.

La v1 se considera completa cuando los Incrementos 1–7 estén validados contra los
**criterios de aceptación globales del PRD (§10)**. El Incremento 8 es distribución.

## Fuera de alcance v1

Multimonitor, captura de ventana/retardo, nube/compartir, selector de fuente, autoactualización,
firma de código, vídeo/GIF y edición avanzada. Ver PRD §12 (roadmap v2+).


## Evolución a v2.0 (solicitud de Toni, 2026-09-12)

Esta petición amplía el alcance histórico del esqueleto y de v1. No implica validar
el empaquetado ni las pruebas manuales anteriores. Se mantiene la entrega incremental,
sin commit ni push hasta confirmación de Toni.

### ✅ V2.1 — Acabados de flecha y sombra en captura
- Implementado: selector triangular (predeterminado), abierto angular y cuerpo afinado.
  Los tres estilos fueron elegidos expresamente por Toni en esta sesión.
- Sombra opcional para nuevas flechas, líneas, rectángulos, rellenos, dibujo libre y texto.
  El pixelado no muestra la opción ni recibe el efecto. Se conserva al deshacer/rehacer.
- Preferencias conservadas durante la captura; no se modifica la persistencia de ajustes.
- Compilación correcta; aviso NU1900 por consulta de vulnerabilidades bloqueada por red.
- Validado manualmente por Toni el 2026-09-12; 130/130 pruebas automáticas superadas.
  Commit y push a `feature-1` autorizados expresamente. Pruebas: `Zass-V2-Pruebas.md`.

### ✅ V2.2 — Distribución del editor de collage y buffer de sesión
- Herramientas arriba, opciones generales a la izquierda, propiedades contextuales a
  la derecha, lienzo central y miniaturas de capturas disponibles abajo.
- Mantener lienzo libre, resolución original, zoom visual e historial de objetos.
- Decisiones confirmadas por Toni: conservar también las capturas copiadas/guardadas
  directamente; copiar/guardar el collage mantiene editor, buffer e historial hasta salir de Zass.
- Implementado: distribución en cinco zonas; Flechas agrupa los tres estilos existentes
  en el panel derecho; propiedades contextuales de texto y selección; tira de miniaturas
  con reinserción por clic, compartiendo el bitmap original. Vaciar lienzo conserva el buffer.
- Límite de memoria existente de 64 millones de píxeles aplicado al buffer; una captura
  directa que exceda el límite se copia/guarda, pero se avisa de que no pudo retenerse.
- Verificado: compilación aislada correcta, 130/130 tests, comprobación WPF de construcción,
  buffer, reinserción, undo, propiedades, exportación, layout mínimo y liberación al cerrar.
- Entrega aprobada por Toni mediante autorización expresa de commit y push a `feature-1`.
  Plan de pruebas: `Zass-V2-Collage-Pruebas.md`.
  Sellos, pasos, rótulos y nuevos estilos/sombras de collage siguen en V2.3/V2.4.

### ✅ V2.3 — Sellos, pasos y rótulos en collage
- Sellos: check, aspa, prohibido, +, -, ! y ?.
- Pasos con número o letra introducidos manualmente; círculo y lágrima lateral.
- Rótulos con texto libre: rectángulo blanco con borde y bocadillo de cómic.
- Objetos editables con sombra opcional, historial y exportación coherente.

### ✅ V2.4 — Texto, flechas y sombras del collage
- Ampliar texto a partir de la referencia: fuente, tamaño, relleno, contorno y sombra.
- Ampliar flechas tomando como referencia estilos continuos/discontinuos, puntas,
  anchura y opacidad, conservando los estilos curvo, recto y acodado existentes.
- Sombra opcional para todos los elementos añadidos; concretar controles de estilo
  con Toni antes de implementar decisiones que los documentos no definan.


**Entrega conjunta V2.3/V2.4 autorizada por Toni:** sellos, pasos, rótulos y estilos
se implementan en el mismo incremento por petición expresa. Incluye siete sellos;
pasos circulares/lágrima con contenido manual (hasta cuatro caracteres); rótulos blancos
con borde o bocadillo; cinco fuentes, negrita/cursiva y contorno blanco para texto;
puntas rellena/abierta/doble y trazo discontinuo en las tres flechas. Tamaño 8–96,
opacidad 10–100 % y sombra opcional por anotación. Propiedades aplicadas a nuevos objetos.

Compilación aislada en `dist/v2-tools`, 130 tests unitarios y comprobaciones WPF de
88 variantes, límites, previsualización/exportación e historial. Sin dependencias nuevas.
Plan manual: `Zass-V2-Herramientas-Pruebas.md`. Validado por Toni; commit, push y merges a dev/main autorizados.


Correcciones tras pruebas de Toni: contorno blanco detrás del relleno para conservar
el color en texto regular; selección automática tras crear objetos o insertar capturas;
movimiento fino con flechas (1 px) y Mayús+flechas (10 px); zoom visible encima del lienzo
con Acercar/Alejar/100 %/Ajustar y Ctrl+rueda. Validado por Toni.
Compilación actual: `dist/v2-fixes`. Comprobación WPF ampliada con regresión del relleno,
selección automática, movimiento por teclado e invariancia del modelo al cambiar zoom.


Corrección de capas solicitada por Toni: capturas siempre debajo de anotaciones,
con orden estable dentro de cada grupo y sin alterar el historial cronológico.
Vista/exportación usan el mismo orden. Nueva compilación: `dist/v2-layers`.
Validado por Toni.


Corrección de captura desde collage: la espera fija de 150 ms se sustituye por
`HideForCaptureAsync`. Desactiva transiciones DWM solo en esta ventana, cierra el popup
de color, oculta, cede al Dispatcher y espera `DwmFlush` fuera del hilo de UI. La interop
está aislada en `WindowComposition`; errores HRESULT se propagan al aviso de captura
existente y recuperan el collage. No modifica la configuración de animaciones del sistema.
DwmFlush sincroniza actualizaciones pendientes de esta aplicación, no todo el escritorio.
Corrección de rastros validada por Toni.
Compilación final: `dist/v2-capture-clean`; 131 tests y comprobaciones WPF correctos.
