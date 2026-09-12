# Validación v2: primer incremento de captura

Estado: validado por Toni el 2026-09-12. Commit y push a `feature-1` autorizados.

## Preparación

Cerrar Zass desde su menú de bandeja y ejecutar la compilación de desarrollo:
`dotnet run --project Zass.App` desde `D:\Zass`.

## Comprobaciones manuales

1. Capturar una región amplia sobre un fondo claro. Seleccionar Flecha: debe aparecer
   Punta triangular por defecto. Dibujar horizontal, vertical, diagonal y en sentido inverso.
2. Seleccionar Punta abierta y Cuerpo afinado; dibujar ambos. Comprobar puntas angulares,
   sin el redondeo anterior, con grosores 1, 3 y 20, y arrastres cortos y largos.
3. Activar Sombra y dibujar otra flecha. Desactivar y dibujar otra: las anteriores conservan
   su aspecto. Repetir con línea, rectángulo, relleno, dibujo libre y texto.
4. Con Sombra activada, seleccionar Pixelar: el control desaparece y el pixelado no tiene
   sombra. Volver a una herramienta de dibujo: la preferencia sigue activada.
5. Deshacer y rehacer objetos con Ctrl+Z/Ctrl+Y: conservan estilo y sombra; un trazo libre
   se deshace entero. Escribir texto con letras A, L, R y espacios: no cambia de herramienta.
6. Copiar y pegar en Paint. Repetir guardando PNG y añadiendo al collage. Verificar puntas
   y sombras, ausencia de barra/marcos y recorte de la sombra en los límites de selección.
7. Repetir a escalado Windows 150 % (cerrar la captura anterior y crear otra). Comparar
   nitidez y tamaño de sombras. Probar una región junto al borde derecho/inferior para
   comprobar que la barra y su desplegable se pueden usar completos.
8. Cambiar a inglés desde Ajustes y abrir otra captura: los nuevos controles se traducen.

La nueva distribución y herramientas de collage pertenecen a los siguientes incrementos.

Verificación automática: compilación correcta, 130/130 pruebas superadas. Aviso NU1900: consulta de vulnerabilidades de NuGet no disponible por restricción de red. Estas pruebas no validan el aspecto visual de WPF.
