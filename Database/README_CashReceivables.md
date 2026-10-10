# Cuentas a cobrar de Caja

Antes de usar la feature, ejecutar **Update_Add_CashReceivables.sql** en la base de GuardeSoftware que utilice el backend. El script crea las tablas y el índice dentro de una transacción; se puede volver a ejecutar. No modifica cuentas, pagos de alquileres ni saldos existentes. Para instalaciones nuevas, las mismas tablas están incluidas en `CreationQuery.sql`.

## Uso

En **Caja → Cuentas a cobrar → Nueva cuenta**, completar un único campo **Persona y concepto** (por ejemplo, `Juan Pérez · Venta de celular en cuotas`), la fecha del acuerdo y el total en pesos. Las notas permiten dejar las condiciones (por ejemplo, seis cuotas mensuales). Cada cobro se registra con fecha, importe y comentario; puede tener un importe diferente al resto.

La cuenta muestra total acordado, cobrado, saldo pendiente y porcentaje. Al alcanzar el total pasa a Cobradas. Los filtros y la búsqueda permiten encontrar cuentas; Actualizar recupera los cambios de otros usuarios. Las cuentas son globales y permanecen al cambiar el mes de Caja.

Se puede editar la cuenta, pero el total no puede quedar por debajo de lo cobrado ni la fecha ser posterior a sus pagos. Un pago incorrecto se elimina con el componente global de confirmación de la aplicación y se vuelve a cargar; al eliminarlo, su importe vuelve al saldo pendiente. Solo se permite eliminar cuentas sin pagos. Si la primera versión de la tabla ya fue instalada, `Update_Add_CashReceivables.sql` migra `debtor` y `concept` a `description` en una única transacción.

Los cobros de este registro **no modifican automáticamente efectivo, bancos ni ingresos de alquileres**. No hay cálculo de intereses, vencimientos automáticos ni calendario de cuotas. El registro requiere conexión al backend: no se encola para sincronización offline y no anuncia un guardado antes de la respuesta del servidor. La API exige el mismo acceso administrativo que la pantalla de Caja.

## Verificación reproducible

Desde la raíz del repositorio:

```powershell
dotnet build tests/CashReceivables.Checks --no-restore -p:UseSharedCompilation=false -nodeReuse:false
dotnet tests/CashReceivables.Checks/bin/Debug/net8.0/CashReceivables.Checks.dll
```

En la primera ejecución, restaurar el proyecto si no existe `obj/project.assets.json`:

```powershell
dotnet restore tests/CashReceivables.Checks
```

El ejecutable usa autenticación integrada contra `.\SQLEXPRESS` por defecto. Acepta otro servidor como primer argumento. Crea una base temporal con nombre aleatorio y la elimina en `finally`; requiere permiso para crear bases y nunca usa la cadena de conexión de la aplicación. Ejecuta 27 comprobaciones: migración repetida, validaciones decimales, saldo inicial, pagos entre meses, reintento sin duplicación, sobrepago, fechas, edición, eliminación, cancelación total y cobros concurrentes. También levanta un servidor HTTP temporal con los controladores reales y autenticación de prueba para comprobar restricciones de acceso, validación de entradas y respuestas 400/401/403/404/409. No inicia Quartz ni otros trabajos del backend. El servidor se detiene al finalizar.
