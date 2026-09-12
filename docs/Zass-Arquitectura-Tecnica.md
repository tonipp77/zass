# Documento Técnico de Arquitectura — Zass

**Versión:** 1.0
**Estado:** Aprobado para desarrollo (v1)
**Stack:** .NET 10 (LTS) + WPF (C#), TFM `net10.0-windows`
**Plataforma:** Windows 10 (1903+) y Windows 11, monomonitor
**Documento relacionado:** Zass-PRD.md

---

## 1. Visión general de la arquitectura

Zass es una aplicación WPF de proceso único que vive en la bandeja del sistema. Su flujo se articula en tres fases:

1. **Captura:** un hook de teclado de bajo nivel detecta el atajo global; se obtiene un bitmap del monitor activo.
2. **Edición (overlay):** se muestra una ventana sin bordes, topmost, a pantalla completa sobre el monitor activo, que pinta el bitmap congelado como fondo y aloja un lienzo vectorial donde el usuario selecciona y anota.
3. **Exportación:** se compone la selección recortada más las anotaciones en un bitmap final que se envía al portapapeles o se escribe en disco.

Se adopta el patrón **MVVM** propio de WPF para la UI de ajustes y la barra de herramientas, y un **modelo de objetos vectoriales con patrón Command** para las anotaciones y el historial deshacer/rehacer.

```
┌──────────────────────────────────────────────────────────┐
│                    Proceso Zass (WPF)                      │
│                                                            │
│  ┌────────────┐   atajo   ┌──────────────────────────┐   │
│  │ TrayIcon / │──────────▶│  GlobalHotkeyService     │   │
│  │ App host   │           │  (low-level keyboard hook)│   │
│  └────────────┘           └───────────┬──────────────┘   │
│         │                             │ dispara captura    │
│         │                             ▼                    │
│         │                  ┌──────────────────────┐        │
│         │                  │  ScreenCaptureService │        │
│         │                  │  (monitor activo,      │        │
│         │                  │   DPI físico)          │        │
│         │                  └───────────┬───────────┘        │
│         │                              │ bitmap congelado    │
│         │                              ▼                    │
│         │                  ┌──────────────────────────┐    │
│         │                  │   OverlayWindow (WPF)     │    │
│         │                  │  ┌─────────────────────┐  │    │
│         │                  │  │ SelectionLayer      │  │    │
│         │                  │  │ AnnotationCanvas    │  │    │
│         │                  │  │ Toolbar (MVVM)      │  │    │
│         │                  │  └─────────────────────┘  │    │
│         │                  │   UndoRedoManager         │    │
│         │                  └───────────┬──────────────┘    │
│         │                              │ exportar            │
│         │                              ▼                    │
│         │                  ┌──────────────────────────┐    │
│         │                  │  ExportService            │    │
│         │                  │  (clipboard / PNG / JPG)  │    │
│         │                  └──────────────────────────┘    │
│         ▼                                                  │
│  ┌────────────────┐    ┌──────────────────────────────┐   │
│  │ SettingsStore  │    │ LocalizationService (es/en)  │   │
│  └────────────────┘    └──────────────────────────────┘   │
└──────────────────────────────────────────────────────────┘
```

---

## 2. Stack y dependencias

- **Runtime:** .NET 10 (LTS), objetivo `net10.0-windows`. Soporte hasta noviembre de 2028, frente a .NET 8/9 que finalizan en noviembre de 2026.
- **UI:** WPF.
- **Interop Win32:** P/Invoke directo o, preferentemente, la biblioteca **CsWin32** (source generator de Microsoft) para generar los bindings de las APIs nativas de forma tipada y mantenible.
- **Bandeja del sistema:** WPF no trae icono de bandeja nativo. Opciones: `H.NotifyIcon` (recomendada, moderna y mantenida) o `Hardcodet.NotifyIcon.Wpf`. Recomendación: **H.NotifyIcon**.
- **Captura de pantalla:** ver sección 4. Recomendación v1: **GDI BitBlt** del DC del monitor por simplicidad y cero dependencias adicionales; `Windows.Graphics.Capture` queda como evolución para v2 si se necesita rendimiento por GPU.
- **Serialización de ajustes:** `System.Text.Json`.
- **Localización:** ver sección 9.

Se evita deliberadamente cualquier dependencia pesada o no esencial para mantener la huella pequeña (RNF-4).

---

## 3. Atajo global de teclado

### 3.1 Mecanismo

Para que el atajo funcione con la app en segundo plano y permita reconfiguración flexible, se usa un **hook de teclado de bajo nivel** (`SetWindowsHookEx` con `WH_KEYBOARD_LL`). Es más flexible que `RegisterHotKey` para validar combinaciones arbitrarias y para futuras necesidades, a cambio de exigir disciplina de rendimiento.

**Alternativa más simple:** `RegisterHotKey` sobre una ventana mensajera oculta. Es menos invasiva y suficiente para una combinación fija como Ctrl+Shift+S. **Recomendación para v1: empezar con `RegisterHotKey`** por robustez y menor riesgo; migrar a hook de bajo nivel solo si la reconfiguración lo exige.

### 3.2 Requisitos de implementación

- El callback del hook (si se usa) debe ser mínimo: detectar la combinación y delegar el trabajo a otro hilo o al dispatcher de la UI; nunca bloquear (RNF-5).
- Tras suspensión/reanudación del sistema, el atajo debe seguir activo. Si se usa `RegisterHotKey`, registrar la escucha de cambios de sesión (`WM_WTSSESSION_CHANGE` / eventos de energía) y re-registrar si procede.
- Si el registro del atajo falla (combinación ocupada), notificar por la UI y conservar el atajo previo (caso límite del PRD).

### 3.3 Servicio

`GlobalHotkeyService` expone:
- `Register(Hotkey hotkey)` / `Unregister()`
- Evento `HotkeyPressed`
- Validación de combinación y resultado de registro (éxito/conflicto).

---

## 4. Captura de pantalla

### 4.1 Determinar el monitor activo

Al dispararse el atajo:
1. Obtener la posición del cursor (`GetCursorPos`).
2. Determinar el monitor que lo contiene (`MonitorFromPoint`) y sus límites (`GetMonitorInfo`).
3. Trabajar con esos límites en **píxeles físicos**.

### 4.2 Obtener el bitmap

**Opción recomendada v1 — GDI BitBlt:**
- `GetDC(NULL)` o DC del monitor, crear DC compatible, `BitBlt` del rectángulo del monitor, volcar a un `Bitmap` y de ahí a `BitmapSource` para WPF.
- Pros: simple, sin dependencias, suficiente para captura estática. Contras: no captura ciertas superficies protegidas por DRM (aceptable en v1).

**Opción v2 — Windows.Graphics.Capture (WinRT):**
- Captura por GPU, moderna, sin parpadeos, soporta más superficies. Contras: más compleja, requiere WinRT interop. Se difiere.

### 4.3 DPI awareness (crítico)

Aunque la v1 es monomonitor, la nitidez con escalado distinto de 100% es un RNF (RNF-2). Medidas obligatorias:

- Declarar el proceso **Per-Monitor DPI Aware v2** en el manifiesto de la aplicación (`app.manifest`), no por código tardío.
- Operar siempre en **píxeles físicos** para capturar y para dimensionar el overlay; convertir a unidades independientes de dispositivo (DIP) de WPF solo en la capa de presentación, usando el factor de escala real del monitor.
- La ventana overlay debe posicionarse y dimensionarse en coordenadas físicas que cubran exactamente el monitor activo.

**Justificación de diseño:** mantener el modelo en píxeles físicos y aislar la conversión a DIP en una única capa evita el descuadre clásico entre lo que el usuario selecciona y lo que se exporta (RNF-3). Este principio debe respetarse aunque complique ligeramente el código de presentación.

---

## 5. La ventana overlay

### 5.1 Características

- `WindowStyle="None"`, `ResizeMode="NoResize"`, `Topmost="True"`, `ShowInTaskbar="False"`, `AllowsTransparency` según necesidad de la capa de oscurecimiento.
- Posicionada y dimensionada para cubrir exactamente el monitor activo en píxeles físicos.
- Captura el foco de teclado y ratón mientras está activa.
- Esc la cierra y libera recursos.

### 5.2 Composición visual por capas (de fondo a frente)

1. **Capa de fondo:** el bitmap congelado del monitor (`Image` a brillo pleno).
2. **Capa de oscurecimiento:** rectángulo semitransparente sobre todo menos la selección (técnica de "agujero": geometría de toda la pantalla menos el rectángulo de selección, o cuatro rectángulos perimetrales).
3. **Capa de selección:** borde y ocho tiradores de redimensionado, indicador de dimensiones.
4. **Capa de anotaciones (`AnnotationCanvas`):** lienzo WPF en modo retenido donde viven las anotaciones vectoriales.
5. **Barra de herramientas flotante:** control WPF anclado junto a la selección, reposicionable.

WPF es modo retenido: el `Canvas` con `Shape`/`UIElement` por anotación da hit-testing, z-order y redibujado gratis, lo que encaja de lleno con el requisito de anotaciones editables.

---

## 6. Modelo de anotaciones (objetos vectoriales editables)

### 6.1 Jerarquía

Clase base abstracta `Annotation` con propiedades comunes: `Id`, `Color`, `Thickness`, `Bounds`, `ZIndex`, y métodos `Render()` (genera/actualiza el `UIElement` WPF), `HitTest(point)`, `Move(delta)`.

Subclases v1:
- `TextAnnotation` (texto, color, tamaño; fuente fija del sistema en v1).
- `ArrowAnnotation` (origen, destino, grosor, color).
- `RectangleAnnotation` (contorno; rect, grosor, color).
- `FilledRectangleAnnotation` (relleno; rect, color de relleno).
- `FreehandAnnotation` (colección de puntos como un único trazo/`Polyline` o `Path`; grosor, color).

Cada anotación se materializa como un `UIElement` (p. ej. `Line`/`Path`/`Rectangle`/`TextBox`) hijo del `AnnotationCanvas`. El modelo lógico y el visual se mantienen sincronizados.

### 6.2 Herramienta de selección y manipulación

- Una herramienta "puntero" activa el modo selección.
- Clic → hit-testing sobre las anotaciones (z-order de arriba a abajo) → selección.
- Selección visible con marco/tiradores ligeros.
- Arrastre → mueve la anotación (genera un `MoveAnnotationCommand`).
- Supr/Retroceso → borra la anotación seleccionada (`RemoveAnnotationCommand`).

*Nota de diseño:* el `FreehandAnnotation` es un único objeto. No hay edición de puntos individuales en v1.

### 6.3 Texto y gestión de foco

El texto se edita en un `TextBox` en línea. Reglas para evitar el solapamiento teclas-herramienta (riesgo del PRD):
- Mientras el `TextBox` tiene el foco, las teclas alfanuméricas escriben en él; los atajos de herramienta de una sola tecla quedan suprimidos.
- Los atajos con modificador (Ctrl+Z, Ctrl+Y, Ctrl+C, Ctrl+S) siguen operativos y se gestionan a nivel de ventana mediante `InputBindings`/`CommandBindings`, comprobando el contexto de edición.
- Clic fuera o Esc confirma el texto como objeto.

---

## 7. Historial deshacer / rehacer (patrón Command)

### 7.1 Diseño

Interfaz `IUndoableCommand` con `Execute()` y `Undo()`. Cada operación mutadora del lienzo se encapsula como comando:
- `AddAnnotationCommand`
- `RemoveAnnotationCommand`
- `MoveAnnotationCommand`
- `EditTextCommand` (cambios confirmados de un texto)

`UndoRedoManager` mantiene dos pilas (`undoStack`, `redoStack`):
- Ejecutar un comando → `Execute()`, push en `undoStack`, limpiar `redoStack`.
- Ctrl+Z → pop de `undoStack`, `Undo()`, push en `redoStack`.
- Ctrl+Y → pop de `redoStack`, `Execute()`, push en `undoStack`.

### 7.2 Reglas

- El historial es por sesión de overlay: se inicializa vacío al abrir la captura y se descarta al cerrar.
- El dibujo libre se registra como **un solo** `AddAnnotationCommand` al terminar el trazo (al soltar el ratón), no por punto. Así Ctrl+Z deshace el trazo completo (alineado con el PRD).
- Mover una anotación genera un único comando al soltar (no uno por cada delta intermedio del arrastre).

---

## 8. Exportación

### 8.1 Composición del bitmap final

1. Tomar la región de selección final en píxeles físicos.
2. Renderizar la jerarquía visual (fondo recortado a la selección + anotaciones dentro de esa región) a un bitmap mediante `RenderTargetBitmap`, respetando el DPI físico para fidelidad de píxel (RNF-3).
3. Recortar al rectángulo de selección.

*Atención:* ocultar de la composición las capas que no deben exportarse (oscurecimiento, tiradores, marco de selección, marcos de selección de anotaciones). Solo fondo + anotaciones confirmadas.

### 8.2 Portapapeles

- Convertir el `RenderTargetBitmap` y colocarlo en el portapapeles como imagen (`Clipboard.SetImage` o `DataObject` con formato bitmap/DIB). Manejar reintento si el portapapeles está bloqueado por otra app (caso límite del PRD).

### 8.3 Disco

- Acción Guardar → `SaveFileDialog` (explorador del sistema) con filtros PNG/JPG y nombre por defecto con marca temporal `Zass_yyyy-MM-dd_HHmmss.png`.
- Codificar con `PngBitmapEncoder` o `JpegBitmapEncoder` (calidad JPG configurable en Ajustes).

---

## 9. Internacionalización (es/en)

- Recursos en archivos `.resx` (`Strings.resx` para inglés base, `Strings.es.resx` para español) o diccionarios de recursos WPF conmutables en caliente.
- **Recomendación:** diccionarios de recursos WPF (`ResourceDictionary` por idioma) intercambiables en tiempo de ejecución para permitir el cambio de idioma sin reiniciar (RF-20).
- Idioma por defecto: el del sistema si es es/en; en otro caso, inglés. Preferencia persistida en ajustes.
- Toda cadena visible debe estar externalizada; cero literales en la UI.

---

## 10. Persistencia de configuración

- Modelo `AppSettings`: atajo, idioma, formato por defecto, calidad JPG, último color, último grosor, arranque con Windows.
- Serialización JSON con `System.Text.Json`.
- Ubicación: `%APPDATA%\Zass\settings.json`.
- Carga al inicio; guardado al cambiar ajustes y al cerrar.
- **Arranque con Windows:** entrada en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` o acceso directo en la carpeta de inicio del usuario (no requiere permisos de administrador). Recomendación: clave Run en HKCU.

---

## 11. Estructura de proyecto propuesta

```
Zass.sln
├── Zass.App/                 (WPF, punto de entrada, tray, overlay, MVVM)
│   ├── app.manifest          (Per-Monitor DPI Aware v2)
│   ├── Views/                (OverlayWindow, SettingsWindow, Toolbar)
│   ├── ViewModels/
│   ├── Resources/            (es/en, iconos)
│   └── App.xaml(.cs)
├── Zass.Core/                (lógica sin dependencia de UI)
│   ├── Annotations/          (modelo de objetos vectoriales)
│   ├── Commands/             (IUndoableCommand, UndoRedoManager)
│   ├── Capture/              (ScreenCaptureService, monitor activo, DPI)
│   ├── Export/               (ExportService)
│   ├── Hotkeys/              (GlobalHotkeyService)
│   └── Settings/             (AppSettings, SettingsStore)
├── Zass.Interop/             (CsWin32 / P-Invoke aislado)
└── Zass.Tests/               (pruebas unitarias)
```

Separar `Zass.Core` sin dependencia de WPF facilita las pruebas del modelo de anotaciones, el historial y los servicios; aísla la interop Win32 en `Zass.Interop`.

---

## 12. Estrategia de pruebas

- **Unitarias (`Zass.Core`):** modelo de anotaciones (hit-testing, mover, bounds), `UndoRedoManager` (secuencias de deshacer/rehacer, límites de pila), serialización de ajustes.
- **Difíciles de automatizar (verificación manual o de integración):** captura DPI a 100% y 150%, fidelidad de exportación píxel a píxel, robustez del atajo tras suspensión, ausencia de fugas al abrir/cerrar el overlay repetidamente.
- **Lista de comprobación manual** alineada con los criterios de aceptación globales del PRD (sección 10 del PRD).

---

## 13. Empaquetado y distribución

- **Instalador:** Inno Setup (gratuito, sencillo, sin dependencia de MSIX). Genera un `.exe` instalable, crea accesos directos y, si el usuario lo elige, la entrada de arranque.
- **Portable:** publicación self-contained o framework-dependent en una carpeta, ejecutable sin instalación. Decidir entre self-contained (mayor tamaño, sin requerir .NET instalado) y framework-dependent (menor tamaño, requiere runtime .NET 10). **Recomendación: self-contained** para el portable, para máxima comodidad del usuario final.
- **Firma de código:** diferida a v2. Sin firma, SmartScreen puede advertir; documentarlo como limitación conocida (riesgo del PRD).
- **Autoactualización:** fuera de v1.

---

## 14. Decisiones técnicas clave (resumen con justificación)

| Decisión | Elección v1 | Razón | Alternativa diferida |
|---|---|---|---|
| Captura | GDI BitBlt | Simple, sin dependencias | Windows.Graphics.Capture (v2, GPU) |
| Atajo global | RegisterHotKey | Robusto para combinación fija | Hook WH_KEYBOARD_LL si reconfig. lo exige |
| Anotaciones | Objetos vectoriales en Canvas WPF | Modo retenido = edición/hit-test/z-order gratis | n/a |
| Historial | Patrón Command + 2 pilas | Estándar, simple, testeable | n/a |
| DPI | Per-Monitor v2 + píxeles físicos | Evita descuadre y borrosidad | n/a |
| Bandeja | H.NotifyIcon | Mantenida y moderna | Hardcodet.NotifyIcon.Wpf |
| i18n | ResourceDictionary conmutable | Cambio de idioma sin reiniciar | .resx |
| Interop | CsWin32 | Bindings tipados, mantenibles | P/Invoke manual |

---

## 15. Notas para el equipo de desarrollo (IA)

- Respetar el principio de **píxeles físicos en el modelo, DIP solo en presentación**. Es la fuente nº1 de bugs de este tipo de app.
- No fijar las anotaciones sobre el bitmap hasta la exportación; mantenerlas como objetos vivos durante toda la sesión del overlay.
- Excluir explícitamente las capas auxiliares (oscurecimiento, tiradores, marcos de selección) del bitmap exportado.
- Gestionar con cuidado el foco de teclado durante la edición de texto para no romper los atajos ni la escritura.
- Liberar todos los recursos gráficos al cerrar el overlay; verificar ausencia de fugas en ciclos repetidos.
- Donde el PRD deja una decisión abierta (comportamiento de las anotaciones al redimensionar la selección, sección 9 del PRD), implementar la opción propuesta (posición absoluta, recorte si quedan fuera) salvo indicación contraria.

## 16. Ampliación feature-1: línea y collage

- `LineAnnotation` sigue el modelo vectorial de la flecha y se materializa como `Line`
  en `AnnotationCanvasController`. Comparte el flujo de borrador y `AddAnnotationCommand`.
- `Zass.Core.Collage.CollageDocument` almacena identificadores y rectángulos en píxeles
  físicos, sin WPF. Usa el historial Command existente con estados inmutables para añadir,
  mover, quitar y vaciar. Valida dimensiones y superficie antes de modificar el documento.
- `CollageWindow` conserva los bitmaps por identificador, incluidos los necesarios para
  deshacer. Los libera al finalizar la sesión. Cerrar la ventana normalmente la oculta;
  una exportación correcta termina la sesión. No se añaden dependencias.
- El lienzo presenta una unidad por píxel de imagen antes de aplicar el zoom mediante
  `LayoutTransform`; las coordenadas del ratón se leen respecto al lienzo transformado y
  se redondean a píxeles enteros. El zoom no modifica el modelo ni la resolución exportada.
- `CollageComposer` crea un visual separado con fondo blanco y bitmaps a tamaño original,
  ajustado a la unión de recortes y renderizado a 96 DPI. No utiliza el lienzo del editor,
  por lo que nunca incluye sus marcos o su espacio auxiliar. Mantiene el orden de inserción.
- `App` es propietario de la ventana de collage. Oculta la ventana antes de capturar,
  cede 150 ms para repintar el escritorio y bloquea capturas reentrantes durante ese paso.
  El overlay entrega el resultado de `ComposeForExport()` al collage; copiar y guardar
  directamente mantienen el flujo existente. La ventana se puede recuperar desde la bandeja.
- La copia del collage reintenta de forma asíncrona cuando el portapapeles está ocupado.
  Los errores se registran y se notifican, conservando la sesión para volver a intentarlo.

### Anotaciones de collage y efecto de pixelado

- `CollageItem` admite una decoración inmutable con tipo, geometría local, color,
  tamaño y texto. Usa el mismo historial de estados que los recortes; mover cambia el
  rectángulo del objeto manteniendo la geometría local. Los límites de exportación
  incluyen todos los objetos. `CollageDecorationRenderer` produce el mismo `Drawing`
  tanto para la imagen vectorial de previsualización como para `CollageComposer`.
- Las flechas curva, recta y acodada se construyen como geometrías WPF. El texto utiliza
  `FormattedText` con Segoe UI. Los objetos siguen siendo independientes hasta exportar;
  la composición nunca incorpora el editor de texto ni el marco de selección.
- El collage confirma los borradores antes de ocultarse, iniciar una captura o exportar.
  El editor de texto conserva el control de las teclas mientras está activo. Una vez
  confirmado, texto y flechas participan en el historial cronológico del documento.
- `PixelationAnnotation` conserva el rectángulo y el tamaño de bloque en píxeles físicos.
  `PixelationEffect` es el algoritmo puro de promediado BGRA, probado con bloques parciales
  y stride con padding. No añade dependencias WPF a Core.
- Al comenzar un pixelado se compone una instantánea del fondo y las anotaciones ya
  confirmadas, excluyendo el oscurecimiento y todos los controles. `PixelationRenderer`
  recorta esa instantánea y genera el efecto durante el arrastre. El controlador conserva
  solo el bitmap resultante de cada rectángulo para reproducirlo idénticamente al rehacer;
  libera la instantánea completa al confirmar y los efectos del historial redo descartado
  al crear un nuevo comando. El bitmap no modifica el fondo original del overlay.
- Los atajos de teclado confirman un trazo activo una sola vez antes de ejecutar acciones,
  evitando incluir borradores sin confirmar al exportar o duplicar operaciones de historial.


## 17. Primer incremento v2: estilos y sombras en captura

`ArrowStyle` y `Annotation.HasShadow` conservan el estilo en el modelo sin WPF.
El controlador captura estas preferencias al iniciar cada objeto, incluido el editor
inline de texto. Undo/redo reutiliza el objeto con sus preferencias originales.

Las flechas cerradas se dibujan como polígonos rellenos; la abierta utiliza extremos
planos y uniones angulares. La cabeza mínima también se convierte de píxeles físicos
a DIP. `DropShadowEffect` se aplica por objeto (negro, opacidad 0,45, desenfoque 4 px,
desplazamiento 3 px hacia abajo/derecha). No se aplica a `PixelationAnnotation`.
La exportación sigue renderizando el mismo canvas de anotaciones, sin controles.
No se añaden dependencias ni se cambian TFM, captura o persistencia.
