# Historial de bauleras: reunificación dentro de 30 días

Ejecutar `Update_Merge_ClientLockerHistory_30Days.sql` completo (incluidos los bloques separados por `GO`) antes de desplegar el backend. El archivo instala el procedimiento usado al asignar bauleras y consolida los registros existentes. No fue aplicado a la base de la aplicación.

Reglas:

- Agrupa exclusivamente por el mismo `client_id` y `locker_id`; no compara números visibles que pueden reutilizarse.
- Une tramos consecutivos cuando el siguiente comienza hasta 30 días después del fin anterior, inclusive. Un segundo más allá del plazo conserva dos tramos.
- Una cadena de tres o más asignaciones queda en una fila si cada separación cumple el plazo.
- Conserva el ID del tramo más antiguo y la fecha inicial más antigua. Si alguno está abierto, el resultado queda activo; en otro caso conserva la última baja.
- Concatena las notas sin truncarlas y archiva todos los originales involucrados en `client_locker_history_merge_archive`, junto con el ID del registro unificado.
- Asignar una baulera que ya tiene un tramo abierto no agrega otra fila. El cierre y la reunificación participan en la transacción de edición/reactivación existente.
- Ejecutar el script nuevamente no vuelve a archivar registros que ya quedaron unificados.

Pruebas de integración, desde la raíz del repositorio:

```powershell
dotnet run --project tests/LockerHistory.Checks/LockerHistory.Checks.csproj -p:BaseOutputPath=C:/Users/fsgbr/Documents/Guarde-Software/.codex_tmp/locker-history-tests/ -p:UseAppHost=false
```

La suite utiliza una base temporal de nombre aleatorio en `.\SQLEXPRESS`, que elimina al terminar. No lee la conexión de la aplicación.
