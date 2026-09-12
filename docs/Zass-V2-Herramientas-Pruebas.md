# Pruebas V2.3/V2.4: herramientas y estilos del collage

Validado por Toni. Commit y push a feature-1, e integración en dev y main autorizados.

## Abrir la entrega

Termina el trabajo abierto y sal de Zass desde bandeja (salir libera la sesión).
Ejecuta `D:\Zass\dist\v2-tools\Zass.exe` y abre Collage.

## Comprobar manualmente

1. Añade una captura. Selecciona Sellos y coloca los siete símbolos por clic. Cambia
   color/tamaño y comprueba que las colocaciones anteriores conservan su estilo.
2. Selecciona Pasos, círculo, clic en lienzo y escribe `1`; confirma con Enter. Repite
   con `A` y con lágrima lateral. No debe incrementarse automáticamente. Máximo 4 caracteres.
3. En Rótulos, escribe una frase con caja blanca con borde. Repite con bocadillo de cómic.
   Cambia fuente y tamaño; comprueba que texto y marco se ajustan sin recortar letras.
4. En Texto prueba las cinco fuentes, negrita, cursiva y contorno blanco sobre una captura
   oscura. Escribe letras, espacios y números sin activar comandos de la ventana.
5. En Flechas prueba curva, recta y acodada; combina punta rellena, abierta y doble con
   trazo continuo/discontinuo. Dibuja en todas direcciones y con arrastres cortos/largos.
6. En cada herramienta activa Sombra y cambia opacidad. Confirma, cambia las opciones y
   crea otro objeto. El anterior conserva su estilo. La sombra queda abajo y a la derecha.
7. Con Seleccionar mueve, quita, deshaz y rehaz cada tipo. Se restaura el objeto completo
   con contenido, posición y estilo. Los controles de estilos afectan solo a nuevos objetos.
8. Copia y guarda PNG: no aparecen controles ni marcos de selección. Las sombras y puntas
   extremas entran completas en la salida. El editor y el buffer siguen disponibles.
9. Cambia zoom (10 %, 50 %, 100 %, 200 %) y prueba el tamaño mínimo de ventana y escalado
   Windows 150 %. El zoom no debe cambiar las dimensiones de la exportación.
10. Cambia a inglés y vuelve a español. Comprueba nombres de herramientas y estilos.

## Verificación automática reproducible

Desde `D:\Zass`, con la compilación de esta entrega cerrada:

```powershell
dotnet build --no-restore -p:OutDir=D:/Zass/dist/v2-tools/
dotnet test --no-restore -p:OutDir=D:/Zass/dist/v2-tools/
dotnet run --project tests/CollageChecks/CollageChecks.csproj -p:OutDir=D:/Zass/dist/v2-tools/
```

Resultados: 130/130 pruebas unitarias; 88 variantes WPF con igualdad de píxeles entre
previsualización y exportación, límites de sombra, historial, editor y buffer.
Aviso NU1900: consulta de vulnerabilidades NuGet bloqueada por red.


## Revisar las correcciones posteriores

La compilación corregida es `D:\Zass\dist\v2-fixes\Zass.exe`. Cierra primero Zass desde
bandeja después de terminar el trabajo de la sesión. Para repetir los comandos automáticos,
usa `dist/v2-fixes` como salida.

1. Texto: escribe HOLA en rojo, tamaño 96, peso normal y contorno blanco. El interior debe
   ser rojo y el borde blanco. Repite con negrita, otra fuente, opacidad y sombra; copia PNG.
2. Crea cada tipo de objeto: tras confirmar debe quedar seleccionado en Seleccionar/mover.
   Arrástralo; pulsa flechas para 1 px o Mayús+flechas para 10 px. Deshaz y rehace movimientos.
   Para crear otro objeto, vuelve a pulsar su herramienta. Repite insertando una miniatura.
3. Las marcas que ya pertenecen a una captura se desplazan con la imagen completa; prueba
   el movimiento independiente con texto, flechas y sellos creados dentro del collage.
4. Usa Acercar/Alejar/100 %/Ajustar encima del lienzo y Ctrl+rueda sobre él. Ajustar debe
   mostrar el contenido completo. Copia antes y después: la resolución debe ser idéntica.
5. Repite con la ventana reducida y escalado Windows 150 %. Confirma que los controles
   nuevos permanecen accesibles y que las teclas no interrumpen la edición de texto.

Regresiones WPF verificadas: superficie de relleno de texto regular conservada con contorno,
selección automática, desplazamiento y undo por teclado, modelo invariable con zoom.


## Verificación del orden de capas

Cierra Zass tras terminar tu sesión y abre `D:\Zass\dist\v2-layers\Zass.exe`.

1. Crea texto, una flecha y un sello antes de insertar una captura.
2. Inserta una captura desde la tira inferior y muévela debajo de esos elementos:
   las anotaciones deben permanecer encima, visibles y seleccionables.
3. Añade otra captura. Debe quedar sobre la primera captura y debajo de las anotaciones.
4. Mueve y quita objetos; deshaz y rehace. Comprueba que la regla se conserva.
5. Copia y guarda PNG: el orden debe coincidir con el lienzo.

Pruebas automáticas añadidas: orden estable por grupos con historial cronológico;
previsualización y píxeles de exportación con sello creado antes de una captura opaca.


## Captura sin restos del collage

Cierra Zass tras terminar la sesión y ejecuta `D:\Zass\dist\v2-capture-clean\Zass.exe`.

1. Abre el collage sobre una ventana con fondo claro y pulsa Nueva captura. Comprueba
   que no quedan textos, bordes ni sombras del editor dentro de la selección; copia y pega.
2. Repite sobre fondo oscuro y sobre un vídeo reproduciéndose, como en la incidencia.
3. Prueba ventana normal y maximizada, cinco capturas seguidas y escalado 150 %.
4. Pulsa Esc para cancelar: vuelve el collage con objetos e historial intactos.
5. Prueba también el atajo global con el collage visible y con el collage ya oculto.

La desaparición de restos requiere comprobación manual: las pruebas de modelo y
renderizado de anotaciones no validan las transiciones del compositor del escritorio.
