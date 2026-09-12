# Validación V2.2: editor de collage y capturas de sesión

Estado: entrega aprobada por Toni mediante autorización expresa de commit y push a `feature-1`.

## Ejecución

1. Termina los trabajos que tengas abiertos en Zass y sal desde el menú de bandeja.
   Salir libera las capturas de esa ejecución.
2. Ejecuta `D:\Zass\dist\validation\Zass.exe`, compilación de esta entrega.
   Alternativamente ejecuta `dotnet run --project Zass.App` desde `D:\Zass` después de cerrar Zass.

## Pasos manuales

1. Captura una región, anótala y cópiala. Abre Collage desde la bandeja: aparece una
   miniatura abajo y el lienzo sigue vacío. Clic en la miniatura añade la captura.
2. Guarda otra captura en PNG. Comprueba que aparece en la tira. Cancela una captura
   y cancela un diálogo de guardado: no deben añadir miniaturas hasta exportar con éxito.
3. Usa Añadir al collage desde captura: aparece una única miniatura nueva y un objeto
   nuevo en el lienzo. Pulsa esa miniatura otra vez: añade otra instancia independiente.
4. Mueve las dos instancias, quita una y prueba deshacer/rehacer. Vacía el lienzo:
   conserva las miniaturas. Deshacer restaura los objetos y sus posiciones.
5. Verifica la distribución: Seleccionar/Flechas/Texto arriba; captura, historial,
   quitar, vaciar y exportar a la izquierda; propiedades a la derecha; lienzo central;
   miniaturas abajo. Flechas muestra curva, recta y acodada. Texto muestra color/tamaño.
6. Dibuja flechas y texto. Escribe letras y espacios sin activar acciones de ventana.
   Comprueba que el historial sigue incluyendo capturas, movimientos y anotaciones.
7. Copia el collage y pégalo en Paint: mantiene el editor abierto, conserva el buffer
   y no incluye paneles ni marcos. Guarda en PNG y repite una modificación/exportación.
8. Oculta/cierra la ventana y recupérala desde bandeja. Debe conservar todo. Una nueva
   captura oculta el editor antes de congelar la pantalla; cancelar conserva el trabajo.
9. Prueba zoom, tamaño mínimo de ventana, ventana maximizada y escalado Windows 150 %.
   Los paneles deben ser utilizables y exportar la misma resolución sin afectar el zoom.
10. Cambia a inglés en Ajustes: títulos, botones y miniaturas se actualizan.
11. Sal de Zass y vuelve a abrir: empieza una sesión vacía.

## Evidencia automática

- Compilación correcta en salida aislada porque la app anterior estaba ejecutándose.
- 130/130 pruebas unitarias existentes superadas.
- Comprobaciones WPF sin mostrar la ventana: construcción, deduplicación del buffer,
  reinserción compartiendo bitmap, vaciar/undo, propiedades contextuales, resolución de
  exportación, distribución a 900×580 y 1280×820 y limpieza al cerrar.
- Aviso NU1900: consulta de vulnerabilidades de NuGet bloqueada por red.

El buffer admite 64 millones de píxeles. Al llenarse, una captura directa se copia/guarda
pero avisa de que no pudo retenerse. Reiniciar Zass libera la sesión.

Los sellos, pasos, rótulos y ampliaciones de texto/flechas/sombra se entregan en los
siguientes incrementos, una vez validada esta base.
