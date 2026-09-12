# PRD — Zass (Herramienta de captura y anotación de pantalla)

**Versión del documento:** 1.0
**Estado:** Aprobado para desarrollo (v1)
**Destinatario:** Equipo de desarrollo (IA)
**Plataforma objetivo:** Windows 10 (1903+) y Windows 11
**Stack acordado:** .NET 10 (LTS) + WPF (C#), TFM `net10.0-windows`

---

## 1. Resumen ejecutivo

Zass es una aplicación de escritorio para Windows que permite capturar una región o la totalidad del monitor activo y anotar la captura **en el sitio**, sobre un overlay congelado que cubre la pantalla, antes de copiarla al portapapeles o guardarla en disco. El modelo de interacción de referencia es el de herramientas como Greenshot, ShareX o Lightshot: el usuario dispara con un atajo global, selecciona, anota con una barra de herramientas flotante y exporta.

El diferenciador funcional de la v1 es que **las anotaciones son objetos vectoriales editables** (se pueden seleccionar, mover y borrar) con un historial de deshacer/rehacer, en lugar de pintura plana fijada sobre el bitmap.

---

## 2. Objetivos y no objetivos

### 2.1 Objetivos de la v1

1. Capturar una región rectangular o el monitor activo completo mediante un atajo global configurable.
2. Mostrar la captura congelada en un overlay que permita redimensionar la selección y anotar sobre ella.
3. Ofrecer un conjunto de herramientas de anotación: texto, flecha, rectángulo (contorno), rectángulo relleno y dibujo libre, cada una con selección de color y grosor.
4. Permitir editar las anotaciones después de crearlas (mover, borrar) con deshacer (Ctrl+Z) y rehacer (Ctrl+Y).
5. Exportar el resultado al portapapeles como imagen y/o guardarlo en disco mediante el explorador de archivos.
6. Residir en la bandeja del sistema y responder al atajo aunque esté en segundo plano.
7. Interfaz disponible en español e inglés, conmutable en ajustes.

### 2.2 No objetivos de la v1 (planificados para v2+)

- Soporte multimonitor simultáneo (capturar varias pantallas a la vez).
- Captura de ventana activa concreta y captura con retardo (delay).
- Subida a la nube y compartir (los iconos de la referencia visual quedan fuera de v1).
- Selector de tipo de fuente para el texto (en v1 se usa una fuente fija del sistema).
- Autoactualización y firma de código (ver sección 11, riesgos).
- Captura de vídeo o GIF.
- Edición avanzada: capas, recorte posterior, desenfoque/pixelado, numeración automática de pasos.

---

## 3. Usuarios y caso de uso principal

**Usuario tipo:** profesional técnico que documenta incidencias, redacta tickets de soporte o prepara material formativo y necesita capturar y anotar pantallas con rapidez.

**Flujo principal (happy path):**

1. El usuario pulsa el atajo global (por defecto Ctrl+Shift+S).
2. La pantalla del monitor activo se "congela" bajo un overlay oscurecido y el cursor cambia a cruz.
3. El usuario arrastra para definir una región rectangular. Mientras arrastra ve las dimensiones en píxeles.
4. Al soltar, aparece la barra de herramientas flotante junto a la selección. La región fuera de la selección permanece atenuada.
5. El usuario ajusta el tamaño de la selección con los tiradores si lo necesita.
6. El usuario elige una herramienta, configura color y grosor, y anota sobre la captura. Puede seleccionar una anotación previa para moverla o borrarla. Ctrl+Z / Ctrl+Y deshacen y rehacen.
7. El usuario pulsa **Copiar** (la imagen anotada va al portapapeles y el overlay se cierra) o **Guardar** (se abre el explorador de archivos para elegir ubicación, nombre y formato).
8. El overlay se cierra y Zass vuelve a la bandeja del sistema.

---

## 4. Requisitos funcionales

Cada requisito lleva un identificador (RF-n) y, donde aplica, criterios de aceptación.

### 4.1 Activación y captura

**RF-1 — Atajo global configurable.**
La captura se inicia con un atajo de teclado global. Por defecto: **Ctrl+Shift+S**. Debe funcionar con la app en segundo plano (minimizada en bandeja).
*Aceptación:* con Zass en bandeja y el foco en cualquier otra aplicación, pulsar el atajo lanza el overlay de captura en menos de 300 ms.

**RF-2 — Configuración del atajo.**
El usuario puede cambiar el atajo desde Ajustes. La app valida que la combinación no esté vacía y advierte si ya está en uso por el sistema cuando sea detectable.
*Aceptación:* tras cambiar el atajo y aceptar, el nuevo atajo dispara la captura y el anterior deja de hacerlo, sin reiniciar la app.

**RF-3 — Captura del monitor activo.**
La captura opera sobre el monitor donde se encuentra el cursor en el instante de pulsar el atajo. No se capturan otros monitores.
*Aceptación:* con dos monitores conectados, la captura cubre únicamente el monitor que contiene el puntero.

**RF-4 — Selección de región rectangular.**
El usuario arrastra para definir un rectángulo. Durante el arrastre se muestra el tamaño actual en píxeles (ancho x alto) cerca del cursor o de la selección.
*Aceptación:* el rectángulo seguir al cursor en tiempo real; al soltar, la selección queda fijada y editable.

**RF-5 — Captura de pantalla completa.**
Existe una acción para seleccionar el monitor activo completo sin arrastrar (por ejemplo, botón en la barra o tecla rápida dentro del overlay).
*Aceptación:* la acción selecciona toda el área del monitor activo como región.

**RF-6 — Redimensionado de la selección.**
La selección muestra ocho tiradores (esquinas y lados) que permiten redimensionarla tras crearla. También debe poder reposicionarse arrastrándola desde el interior.
*Aceptación:* arrastrar un tirador cambia el tamaño respetando los límites del monitor; arrastrar el interior mueve la selección completa con sus anotaciones.

**RF-7 — Cancelar captura.**
La tecla Esc cancela el overlay sin copiar ni guardar y devuelve la app a la bandeja.

### 4.2 Anotaciones

**RF-8 — Herramientas de anotación.**
Conjunto mínimo de v1:
- **Texto:** se hace clic para colocar un cuadro de texto editable.
- **Flecha:** se arrastra de origen a destino.
- **Rectángulo (contorno):** se arrastra para definir el área; solo borde.
- **Rectángulo relleno:** se arrastra; el interior se rellena con el color elegido.
- **Dibujo libre:** trazo a mano alzada siguiendo el cursor mientras se mantiene pulsado.

**RF-9 — Color configurable.**
Toda herramienta permite elegir color. Debe haber una paleta rápida de colores comunes más un selector de color completo. El color elegido persiste entre anotaciones dentro de la misma sesión de overlay.

**RF-10 — Grosor configurable.**
Toda herramienta de trazo (flecha, rectángulos, dibujo libre) y el tamaño del texto permiten elegir grosor/tamaño. Rango razonable propuesto: 1 a 20 px para trazo; 8 a 72 pt para texto.

**RF-11 — Anotaciones como objetos editables.**
Cada anotación es un objeto seleccionable independiente. Una herramienta de selección (puntero) permite:
- Seleccionar una anotación haciendo clic sobre ella (hit-testing).
- Moverla arrastrándola.
- Borrarla con la tecla Supr o Retroceso.
*Nota de diseño:* el dibujo libre es un único objeto-trazo; no se editan sus puntos individualmente. Borrarlo o deshacerlo afecta al trazo completo.

**RF-12 — Deshacer / rehacer.**
Ctrl+Z deshace la última operación (crear, mover, borrar). Ctrl+Y la rehace. La pila de historial cubre todas las operaciones realizadas desde que se abrió el overlay actual.
*Aceptación:* tras crear tres anotaciones y pulsar Ctrl+Z tres veces, el lienzo queda sin anotaciones; tres Ctrl+Y las restauran en orden.

**RF-13 — Texto: edición.**
El cuadro de texto se edita en línea. Mientras está en modo edición, las teclas de texto escriben en él (no disparan atajos de anotación). Al hacer clic fuera o pulsar Esc, el texto se confirma como objeto.
*Nota:* en v1 la fuente es fija (fuente del sistema). Solo color y tamaño son configurables.

### 4.3 Exportación

**RF-14 — Copiar al portapapeles.**
Acción **Copiar**: aplana la selección recortada más sus anotaciones en un bitmap y lo coloca en el portapapeles como imagen. Cierra el overlay.
*Aceptación:* tras copiar, pegar en una aplicación que acepte imágenes (p. ej. un editor o un chat) inserta la captura anotada con fidelidad de píxel.

**RF-15 — Guardar en disco.**
Acción **Guardar**: abre el diálogo del explorador de archivos del sistema (Guardar como) con selección de carpeta, nombre y formato. Cierra el overlay tras guardar.
*Aceptación:* el archivo resultante contiene la selección anotada en el formato elegido.

**RF-16 — Formatos de salida.**
PNG (por defecto) y JPG. PNG conserva transparencia si la hubiera; JPG con calidad razonable por defecto.

**RF-17 — Nombre de archivo propuesto.**
El diálogo de guardado sugiere un nombre por defecto con marca temporal, p. ej. `Zass_2026-06-19_142233.png`. El usuario puede cambiarlo.

### 4.4 Aplicación, residencia e idioma

**RF-18 — Icono en bandeja del sistema.**
Zass reside en la bandeja con un menú contextual que incluye, como mínimo: Capturar ahora, Ajustes, Acerca de, Salir.

**RF-19 — Arranque con Windows (opcional).**
Ajuste para iniciar Zass automáticamente con el inicio de sesión de Windows. Desactivado por defecto.

**RF-20 — Idioma de interfaz.**
Español e inglés, conmutable en Ajustes. El idioma por defecto sigue al del sistema operativo cuando sea español o inglés; en cualquier otro caso, inglés.

**RF-21 — Persistencia de preferencias.**
La app recuerda entre sesiones: atajo configurado, idioma, formato de guardado por defecto, último color y grosor usados, y la opción de arranque con Windows.

---

## 5. Requisitos no funcionales

**RNF-1 — Rendimiento de captura.** Desde la pulsación del atajo hasta el overlay visible: objetivo < 300 ms en hardware de gama media.

**RNF-2 — Nitidez DPI.** La captura y las anotaciones deben verse nítidas en monitores con escalado distinto de 100% (p. ej. 150%). El proceso debe declararse Per-Monitor DPI Aware v2 y operar en píxeles físicos. (Ver documento de arquitectura.)

**RNF-3 — Fidelidad de exportación.** La imagen exportada (portapapeles o archivo) debe coincidir píxel a píxel con la selección anotada que ve el usuario.

**RNF-4 — Consumo en reposo.** Con la app en bandeja sin actividad, uso de CPU cercano a cero y huella de memoria modesta (objetivo orientativo < 80 MB).

**RNF-5 — Robustez del atajo.** El hook de teclado no debe bloquear ni ralentizar el sistema, ni dejar de funcionar tras suspensión/reanudación del equipo.

**RNF-6 — Estabilidad.** Cancelar (Esc) o cerrar el overlay debe liberar todos los recursos gráficos sin fugas; abrir y cerrar la captura repetidas veces no degrada el rendimiento.

---

## 6. Especificación de la interfaz del overlay de captura

**Estado de selección:**
- Fondo: la captura congelada del monitor, con una capa de oscurecimiento semitransparente encima.
- La región seleccionada se muestra a brillo pleno (sin oscurecer); el resto, atenuado.
- Borde de selección visible con ocho tiradores de redimensionado.
- Indicador de dimensiones (ancho x alto en px) visible durante el dibujo y el redimensionado.

**Barra de herramientas flotante** (anclada junto a la selección, reposicionable si no cabe):
- Herramienta selección/puntero.
- Texto.
- Flecha.
- Rectángulo (contorno).
- Rectángulo relleno.
- Dibujo libre.
- Selector de color (paleta rápida + selector completo).
- Selector de grosor/tamaño.
- Separador.
- Copiar.
- Guardar.
- Cerrar/cancelar.

La barra debe indicar visualmente la herramienta activa y el color/grosor actuales.

---

## 7. Atajos de teclado dentro del overlay

| Acción | Tecla |
|---|---|
| Cancelar captura | Esc |
| Deshacer | Ctrl+Z |
| Rehacer | Ctrl+Y |
| Borrar anotación seleccionada | Supr / Retroceso |
| Copiar al portapapeles | Ctrl+C o botón Copiar |
| Guardar | Ctrl+S o botón Guardar |

*Nota:* cuando un cuadro de texto está en edición, las teclas alfanuméricas escriben en el texto y no activan herramientas; los atajos con modificador (Ctrl+Z, etc.) siguen operativos.

---

## 8. Pantalla de Ajustes

Contenido mínimo:

1. **Atajo global** (captura y validación de combinación).
2. **Idioma** (Español / English).
3. **Formato de guardado por defecto** (PNG / JPG) y calidad JPG.
4. **Arrancar con Windows** (on/off).
5. **Restablecer valores por defecto.**

---

## 9. Casos límite y comportamiento esperado

- **Atajo en conflicto con otra app:** si el registro del atajo global falla porque otra aplicación lo tiene tomado, Zass avisa al usuario y mantiene el atajo anterior o pide uno nuevo. No falla en silencio.
- **Selección de tamaño cero o mínimo:** si el usuario hace solo clic sin arrastrar, no se crea selección; el overlay permanece a la espera. Definir tamaño mínimo de selección (p. ej. 5x5 px) por debajo del cual se ignora.
- **Anotación fuera de la selección:** las anotaciones se restringen visualmente al área de la captura; al exportar solo se incluye lo que cae dentro de la selección final.
- **Redimensionar la selección con anotaciones ya creadas:** definir comportamiento. Propuesta de v1: las anotaciones mantienen su posición absoluta; si la selección se encoge y deja una anotación fuera, esa parte se recorta en la exportación. (Decisión a confirmar por el equipo si se prefiere escalar las anotaciones con la selección.)
- **Suspensión del equipo:** tras reanudar, el atajo global debe seguir funcionando sin reiniciar la app.
- **Cambio de resolución o escalado durante la sesión:** fuera del overlay, la app debe adaptarse al nuevo escalado en la siguiente captura.
- **Portapapeles ocupado o bloqueado por otra app:** reintentar o informar al usuario; no bloquear la UI.

---

## 10. Criterios de aceptación globales de la v1

La v1 se considera completa cuando:

1. El atajo por defecto y uno configurado por el usuario lanzan la captura con la app en segundo plano.
2. Se puede capturar región y pantalla completa del monitor activo, con la selección redimensionable y movible.
3. Las cinco herramientas de anotación funcionan con color y grosor configurables.
4. Las anotaciones se pueden seleccionar, mover y borrar; Ctrl+Z y Ctrl+Y funcionan sobre toda la sesión.
5. Copiar coloca la imagen anotada en el portapapeles; Guardar abre el explorador y exporta a PNG/JPG con nombre por defecto con marca temporal.
6. La interfaz se muestra correctamente en español e inglés y conmuta sin reiniciar.
7. La captura y las anotaciones se ven nítidas con escalado de pantalla al 150%.
8. La app reside en bandeja, opcionalmente arranca con Windows, y persiste las preferencias entre sesiones.

---

## 11. Riesgos y dependencias

- **Atajo Ctrl+Shift+S:** combinación poco conflictiva, pero no imposible de colisionar con otras apps. Mitigación: detección de fallo de registro y aviso al usuario (caso límite ya cubierto).
- **DPI mixto futuro:** aunque la v1 es monomonitor, el diseño DPI-aware debe quedar bien planteado para no rehacer todo al incorporar multimonitor en v2.
- **Firma de código:** sin firma, Windows SmartScreen puede advertir al ejecutar el instalador. La firma tiene coste anual de certificado y se difiere a v2; debe comunicarse al usuario final como limitación conocida de la v1.
- **Edición de texto vs. atajos de herramienta:** el solapamiento de teclas durante la edición de texto es una fuente típica de bugs; requiere gestión cuidadosa del foco (detallado en arquitectura).

---

## 12. Roadmap posterior (referencia, fuera de alcance v1)

- Multimonitor y captura del escritorio virtual completo.
- Captura de ventana activa y captura con retardo.
- Subida a la nube y compartir (los iconos de la referencia visual).
- Selector de fuente para texto; más herramientas (numeración de pasos, resaltado, desenfoque/pixelado, recorte posterior).
- Autoactualización y firma de código.
- Captura de vídeo/GIF.

## 13. Ampliación solicitada: feature-1 (2026-09-08)

Esta petición amplía el conjunto de herramientas y destinos de exportación de v1.
No incluye recortar posteriormente una imagen importada: cada recorte se obtiene mediante
la selección de pantalla existente, con sus anotaciones confirmadas.

- **RF-22 — Línea:** segmento sin punta, dibujado por arrastre, con el mismo selector de
  color y grosor de 1–20 píxeles que la flecha. Botón propio y atajo L. Una línea completa
  equivale a un comando: Ctrl+Z la elimina y Ctrl+Y la restaura con su estilo.
- **RF-23 — Destino del recorte:** tras seleccionar y anotar, el usuario elige Copiar,
  Guardar o Añadir recorte al collage. Añadir cierra el overlay solo tras aceptar el recorte.
- **RF-24 — Collage libre:** ventana con recortes movibles por arrastre, conservando sus
  dimensiones originales. Nuevos recortes se colocan debajo de los existentes; en
  solapamientos, los añadidos después quedan delante. Permite quitar, vaciar y deshacer/
  rehacer; cada movimiento se registra una vez al soltar. Zoom solo de visualización.
- **RF-25 — Sesión y salida:** el collage persiste en memoria mientras Zass siga abierto.
  Cerrar/ocultar la ventana permite continuar desde el menú de bandeja. Nueva captura
  oculta el collage antes de capturar. Cancelar una captura conserva el collage anterior.
  Terminar y copiar / Terminar y guardar exportan una sola imagen y cierran la sesión
  únicamente tras el éxito. Cancelar Guardar o un error de exportación conserva el trabajo.
  Formatos PNG/JPG, con las preferencias de exportación existentes.
- **Salida:** unión rectangular ajustada a los recortes, espacios blancos y resolución
  original, sin zoom, marcos de selección ni controles. No hay persistencia de proyecto
  entre ejecuciones. Límite técnico de 64 millones de píxeles en la composición y en los
  recortes retenidos con su historial; dimensión máxima 32767 píxeles por lado.

**Decisiones confirmadas por Toni:** lienzo libre y conservación solo durante la sesión.
**Aceptación:** ejecutar `Zass-Feature-1-Pruebas.md`; pendiente de validación manual.

### Ampliación de collage y pixelado

- **RF-26 — Flechas en collage:** tres estilos vectoriales rellenos: curva con cola
  afinada (referencia aportada por Toni), recta y acodada. El clic inicia la flecha;
  arrastrar con el botón izquierdo muestra la previsualización y soltar confirma el
  objeto con la punta en el extremo del arrastre. Color y grosor configurables para
  nuevas flechas. Un clic sin arrastre no añade nada.
- **RF-27 — Texto en collage:** seleccionar Texto y hacer clic para escribir en el
  lienzo, con color y tamaño configurables. Enter, Esc o clic fuera confirman el texto;
  el texto vacío se descarta. Mientras se escribe, Ctrl+Z/Ctrl+Y pertenecen al editor;
  tras confirmar actúan sobre el objeto completo. Antes de capturar/exportar se confirma
  el texto pendiente.
- **RF-28 — Historial único:** recortes, flechas, texto, movimientos y borrados comparten
  un único historial cronológico. Ctrl+Z/Ctrl+Y eliminan/restauran objetos completos con
  su estilo y posición. Seleccionar/mover y Quitar objeto funcionan también con flechas
  y texto. Se exportan todos los objetos en su orden de inserción, incluidos los situados
  fuera de los recortes. Las flechas no se anclan automáticamente al mover las imágenes.
- **RF-29 — Pixelado en recortes:** herramienta Pixelar (P). Arrastrar define un
  rectángulo cuyo contenido se reemplaza por bloques de color medio, incluidas las
  anotaciones previas situadas debajo. Tamaño de bloque de 8 a 64 píxeles, por defecto 24.
  El efecto se confirma al soltar, se deshace/rehace completo y se conserva al copiar,
  guardar o añadir al collage. No depende del color activo. Las anotaciones añadidas
  después quedan encima del efecto.

El pixelado deja de estar diferido al roadmap posterior para esta rama. Su intensidad
se debe comprobar visualmente sobre el contenido concreto; no equivale a una redacción
irreversible de información sensible (para ello existe el rectángulo opaco).


## 14. Evolución a v2.0 solicitada el 2026-09-12

La referencia visual es orientativa; el alcance es la petición de Toni, no los menús
ni servicios de la aplicación fotografiada. Desarrollo por incrementos en el plan.

- **RF-30 — Flechas en captura:** estilos triangular relleno (predeterminado), abierto
  angular y cuerpo afinado, elegidos por Toni. Selector para nuevas anotaciones.
- **RF-31 — Sombra en captura:** interruptor para nuevas anotaciones, incluido texto,
  salvo pixelado. Inicialmente desactivado; se conserva entre herramientas de la captura.
  El historial conserva el estilo individual. Se recorta en el límite de exportación.
- **RF-32 — Editor de collage:** herramientas arriba, opciones generales a la izquierda,
  propiedades de herramienta a la derecha, lienzo central y buffer de capturas abajo.
- **RF-33 — Sellos:** check, aspa, prohibido, +, -, ! y ?.
- **RF-34 — Pasos manuales:** número o letra introducido por el usuario, círculo o lágrima lateral.
- **RF-35 — Rótulos:** texto libre en caja blanca con borde o bocadillo de cómic.
- **RF-36 — Estilos de collage:** ampliar texto y flechas según referencias; sombra opcional
  en los elementos añadidos. Preservar objetos editables e historial hasta exportar.

RF-30/31 implementados y validados por Toni el 2026-09-12. RF-32 implementado y
entrega aprobada por Toni para commit y push. RF-33 a RF-36 pendientes.

### Sesión de collage v2: decisiones confirmadas por Toni

Esta ampliación sustituye la finalización automática de RF-25: copiar/guardar el collage
mantiene el editor, sus objetos, historial y capturas hasta salir de Zass. Cerrar la ventana
la oculta. Vaciar lienzo solo quita objetos y puede deshacerse; conserva las miniaturas.
Todas las capturas exportadas correctamente desde el overlay (copiar, guardar o añadir
al collage) quedan en memoria, incluidas sus anotaciones. Cancelar no retiene una captura.
La tira inferior permite insertar otra instancia por clic, a resolución original. Añadir
al collage sigue colocando el recorte directamente además de retenerlo en la tira.

El buffer mantiene el límite existente de 64 millones de píxeles. Reinsertar no duplica
el bitmap ni consume ese presupuesto otra vez. Si una captura directa no cabe, la copia
o guardado sigue siendo válido y se notifica que no pudo conservarse en el buffer.
El lienzo mantiene su límite independiente de superficie y dimensión de RF-24/25.
