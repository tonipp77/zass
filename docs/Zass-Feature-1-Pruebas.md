# Validación de feature-1: línea y collage

Estado: implementado, pendiente de validación de Toni. Rama: `feature-1`.

## Comprobación automática

Desde `D:\Zass`:

```powershell
dotnet build
dotnet test
```

Resultado de desarrollo: compilación sin errores ni avisos, 130 tests correctos.
También se ejecutó una comprobación WPF sin mostrar ventanas: construcción del editor,
incorporación de recortes y composición exacta de píxeles con bitmaps a 96/144 DPI,
espacios blancos, solapamiento y deshacer. Esto no valida la interacción real de escritorio.
La comprobación WPF ampliada cubre los tres estilos de flecha y sus extremos en varias
direcciones, la confirmación/exportación de texto y el pixelado al 150% con origen de
monitor desplazado y resultado idéntico al deshacer/rehacer.

## Preparación

Cierra cualquier Zass que estuviera ejecutándose para evitar conflictos con el atajo.
Ejecuta `dotnet run --project Zass.App` desde esta rama. Usa el atajo que tengas configurado
(por defecto Ctrl+Shift+S). Abre Paint u otra aplicación donde pegar las imágenes.

## Pasos manuales

1. **Línea:** captura una región y pulsa L o el botón de línea diagonal. Elige color y
   grosor. Dibuja líneas horizontal, vertical y diagonal, también en sentido inverso.
   Deben aparecer sin punta. Un clic sin arrastre no debe añadir una anotación.
2. **Historial mixto:** dibuja una línea, una flecha y otra línea con un estilo distinto.
   Pulsa Ctrl+Z tres veces: desaparece cada objeto completo. Pulsa Ctrl+Y tres veces:
   reaparecen en orden con sus colores y grosores. Durante la escritura de texto, la L
   debe escribir normalmente. Termina el texto antes de probar los atajos de herramienta.
3. **Exportación directa:** copia una selección anotada, pégala y comprueba sus límites,
   colores y grosores. Repite guardando en PNG y JPG. Los nuevos botones no deben romper
   las herramientas, copiar o guardar existentes.
4. **Primer collage:** en otra captura, selecciona una región, añade una anotación y pulsa
   el botón cuyo tooltip dice «Añadir recorte al collage». Se cierra el overlay y aparece
   el editor con el recorte anotado, sin oscurecimiento, tiradores ni barra.
5. **Más capturas:** pulsa «Nueva captura». La ventana del collage desaparece antes de
   congelar la pantalla. Añade otro recorte de tamaño diferente y repite con un tercero.
   Los anteriores se conservan. Lanza otra captura y pulsa Esc: regresa el collage sin
   cambios. También prueba copiar o guardar directamente esa nueva captura.
6. **Lienzo libre:** arrastra los tres recortes, deja espacios y solapa dos. Se mueven
   enteros y los más recientes quedan delante. Cambia el zoom y vuelve a moverlos: no
   deben saltar ni cambiar de tamaño real. Quita uno con Supr; Ctrl+Z lo recupera.
   Prueba deshacer/rehacer un movimiento y «Vaciar lienzo», seguido de Ctrl+Z.
7. **Continuar la sesión:** oculta el editor o ciérralo con X. Abre «Collage» desde el
   menú de bandeja. Deben mantenerse posiciones y recortes. Cerrar Zass por completo sí
   termina la sesión; no se guarda un proyecto para otro día.
8. **Terminar:** usa «Terminar y copiar» y pega la imagen. Se cierra el collage y la imagen
   contiene todos los recortes, con espacios blancos, sin marcos ni controles ni margen
   auxiliar exterior. El menú Collage abre ahora una sesión vacía. Crea otro collage y
   prueba «Terminar y guardar»: cancelar el diálogo conserva el trabajo; guardar en PNG
   o JPG lo termina. Comprueba las dimensiones indicadas en el editor frente al archivo.
9. **DPI e idioma:** repite línea, movimiento y exportación al 100% y al 150% de escalado.
   Comprueba nitidez y posición del cursor. Cambia a inglés desde Ajustes con un collage
   pendiente: sus controles y el menú de bandeja deben actualizarse y conservar el contenido.

Si falla algún paso, indica el número, el escalado, la acción y el resultado observado.
Toni autorizó expresamente commit y push a `feature-1` el 2026-09-09.
La validación manual de esta lista sigue pendiente de registro.

## Ampliación: flechas, texto y pixelado

10. **Flechas del collage:** añade dos recortes. Elige Flecha curva y un color. Haz clic,
    mantén el botón izquierdo y arrastra hasta el segundo recorte: la flecha debe crecer
    en vivo y su punta quedar donde sueltas. Repite con Flecha recta y Flecha acodada,
    con colores y grosores distintos. Prueba arrastres hacia izquierda, derecha, arriba
    y abajo, y un clic sin arrastre (no debe crear objeto).
11. **Texto:** selecciona Texto, elige color/tamaño y haz clic en el lienzo. Escribe
    «Paso 1: revisión», confirma con Enter y repite confirmando con Esc y clic fuera.
    Un texto vacío no debe generar objeto. Mientras escribes, Supr y Ctrl+Z deben
    editar el texto; después de confirmar, Ctrl+Z elimina el objeto completo y Ctrl+Y
    lo restaura. Escribir letras no debe activar herramientas.
12. **Historial mixto:** añade una imagen, una flecha y un texto; mueve la flecha con
    Seleccionar/mover y quita el texto. Deshaz las cinco acciones y reházlas: se deben
    respetar orden, posición, color y tamaño. Vaciar lienzo seguido de Ctrl+Z recupera
    todos los objetos. Las flechas mantienen su posición al mover una imagen.
13. **Salida del collage:** coloca una flecha y un texto parcialmente fuera de los
    recortes. Copia o guarda: se incluyen completos, sin marcos ni editor de texto.
    Repite con otro zoom. Inicia una nueva captura mientras escribes: al regresar,
    el texto confirmado debe seguir en el collage.
14. **Pixelado:** en una captura con texto visible, pulsa P o Pixelar. Arrastra un
    rectángulo sobre el texto. Deben verse bloques, sin relleno del color elegido para
    dibujar. Cambia Bloques (8–64) y dibuja otro rectángulo; comprueba la intensidad.
    Repite de derecha a izquierda y de abajo arriba, y cerca de los bordes de selección.
15. **Capas e historial del pixelado:** dibuja una anotación, después pixélala. Deben
    pixelarse tanto el fondo como esa anotación. Ctrl+Z elimina el rectángulo pixelado
    entero y muestra el contenido anterior; Ctrl+Y devuelve exactamente el mismo efecto.
    Añade una flecha después: queda encima. Copia, guarda y añade al collage para comprobar
    que el efecto se conserva sin zonas sin pixelar ni desplazamientos.
16. **DPI y privacidad:** repite el pixelado y los conectores al 100% y 150%. Comprueba
    que los bordes coinciden y revisa la legibilidad en la imagen exportada. Aumenta el
    tamaño de bloque si es necesario. El pixelado puede conservar patrones reconocibles;
    para ocultación irreversible utiliza el rectángulo opaco.
