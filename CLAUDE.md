# CLAUDE.md — Instrucciones de proyecto para Zass

Este archivo lo lee Claude Code en cada sesión. Define el contexto, las reglas y el flujo de trabajo del proyecto. Respétalo en todo momento.

## Qué es Zass

Zass es una aplicación de escritorio para Windows (.NET 10 + WPF, C#) para capturar una región o el monitor activo completo y anotar la captura **en el sitio**, sobre un overlay congelado que cubre la pantalla, antes de copiarla al portapapeles o guardarla en disco. Modelo de referencia: Greenshot / ShareX / Lightshot. El diferenciador es que las anotaciones son **objetos vectoriales editables** (seleccionables, movibles, borrables) con deshacer/rehacer, no pintura plana.

## Documentos de referencia

En la carpeta `/docs` están las especificaciones completas. **Léelas antes de cualquier trabajo no trivial** y trátalas como fuente de verdad:

- `docs/Zass-PRD.md` — requisitos funcionales, criterios de aceptación, casos límite.
- `docs/Zass-Arquitectura-Tecnica.md` — decisiones técnicas, modelo de objetos, estructura.
- `docs/Zass-Plan-Incremental.md` — **mapa de ejecución**: orden de incrementos y su estado. Consúltalo al inicio de cada sesión, retoma por el primer incremento no completado y actualiza su estado al terminar.

Si algo en una petición contradice estos documentos, señálalo antes de implementarlo; no lo resuelvas en silencio.

## Stack y restricciones inquebrantables

- **.NET 10 (LTS) + WPF (C#), TFM `net10.0-windows`.** No cambies de framework ni de versión sin que se te pida. (.NET 10 es la versión LTS vigente, con soporte hasta noviembre de 2028.)
- **Solo Windows.** WPF solo compila y se ejecuta en Windows. No introduzcas dependencias cross-platform.
- **Monomonitor (v1).** Se captura el monitor donde está el cursor al pulsar el atajo. No implementes multimonitor.
- **Per-Monitor DPI Aware v2** declarado en `app.manifest`, no por código tardío.
- Mantén la huella pequeña. No añadas dependencias pesadas ni innecesarias. Dependencias previstas: CsWin32 para interop, H.NotifyIcon para la bandeja, System.Text.Json para ajustes. Cualquier otra, justifícala antes.

## Principios de arquitectura (críticos)

1. **Píxeles físicos en el modelo, DIP solo en la capa de presentación.** Es la fuente número uno de bugs en este tipo de app. Captura, posicionado del overlay y exportación trabajan en píxeles físicos; la conversión a unidades de WPF se aísla en un único punto.
2. **Las anotaciones son objetos vectoriales vivos** en un `Canvas` de WPF durante toda la sesión del overlay. No se fijan sobre el bitmap hasta la exportación.
3. **Patrón Command para deshacer/rehacer** (dos pilas). El dibujo libre y los movimientos se registran como un único comando, no por punto ni por delta.
4. **Al exportar, excluye las capas auxiliares** (oscurecimiento, tiradores, marcos de selección). Solo fondo recortado + anotaciones confirmadas. La salida debe coincidir píxel a píxel con lo que ve el usuario.

## Estructura del proyecto

```
Zass.sln
├── Zass.App/        WPF: punto de entrada, tray, overlay, vistas, MVVM, app.manifest
├── Zass.Core/       Lógica sin dependencia de UI: anotaciones, comandos, captura, export, hotkeys, settings
├── Zass.Interop/    Interop Win32 aislada (CsWin32 / P-Invoke)
└── Zass.Tests/      Pruebas unitarias de Zass.Core
```

Mantén `Zass.Core` y `Zass.Interop` sin dependencia de WPF para que sean testeables y la interop quede contenida.

## Convenciones de código

- C# moderno, nullable habilitado, `async`/`await` donde aporte.
- Nombres de identificadores y comentarios de código en inglés. Mensajes y textos de UI externalizados a recursos es/en, **cero literales de UI en el código**.
- Un servicio por responsabilidad (captura, hotkey, export, settings). Inyección de dependencias simple.
- Nada de capturar y tragar excepciones en silencio. Errores relevantes para el usuario, a la UI; el resto, registrados.

## Comandos

```bash
dotnet build           # compilar
dotnet run --project Zass.App   # ejecutar la app
dotnet test            # pruebas
```

Tras cada cambio significativo, **compila** y resuelve los errores antes de continuar.

## Flujo de trabajo esperado

- Trabaja en incrementos pequeños y compilables. No generes el proyecto entero de golpe.
- **Tú no puedes ver la pantalla.** No puedes verificar la captura, la nitidez DPI, el resultado del portapapeles ni el comportamiento del atajo. Implementa, compila, y al terminar una tarea entrégame una **lista de verificación manual** con los pasos exactos que debo ejecutar yo para validar lo que tú no puedes.
- Para decisiones de diseño no cubiertas por los documentos, pregúntame antes en lugar de asumir.
- Usa Git: commits pequeños y descriptivos por incremento.

## Errores típicos a evitar

- Descuadre o borrosidad por no respetar el principio de píxeles físicos / DPI v2.
- Incluir capas auxiliares en la imagen exportada.
- Bloquear el sistema o la UI desde el callback del atajo.
- Romper la escritura de texto o los atajos por mala gestión del foco (relevante cuando se añadan anotaciones de texto).

## Alcance actual

Estamos construyendo el **esqueleto vertical** (ver el prompt inicial / la tarea en curso). **No implementes todavía** anotaciones, barra de herramientas de dibujo, diálogo de guardado, pantalla de ajustes, conmutación de idioma ni persistencia avanzada. Eso vendrá en incrementos posteriores una vez validado el esqueleto.
