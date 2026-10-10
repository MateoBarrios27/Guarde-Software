# Imputación de pagos: saldo anterior, intereses, abono

## Regla

Cada crédito se imputa en este orden, tomando como referencia **el mes de su fecha**:

1. Capital de todos los períodos anteriores, del más antiguo al más reciente.
2. Intereses pendientes registrados hasta ese mes, del más antiguo al más reciente.
3. Abono del mes del crédito.
4. Si sobra dinero, períodos futuros en orden cronológico. El importe no aplicado se conserva como saldo a favor.

Los intereses no forman parte del capital mostrado como “Saldo anterior”. Un
crédito de septiembre puede cancelar alquiler de agosto sin cancelar los intereses
registrados en agosto. Un crédito fechado en agosto sigue pagando los intereses
de agosto antes del abono de agosto. Agregar un mes futuro no cambia la referencia
temporal de los créditos anteriores.

Ejemplo de septiembre de 2026 (sin comisión ni nuevo recargo):

| Pago acumulado | Capital anterior pendiente | Intereses pendientes | Abono pendiente | Deuda |
|---:|---:|---:|---:|---:|
| 0 | 232.000 | 48.700 | 232.000 | 512.700 |
| 170.000 | 62.000 | 48.700 | 232.000 | 342.700 |
| 232.000 | 0 | 48.700 | 232.000 | 280.700 |
| 270.000 | 0 | 10.700 | 232.000 | 242.700 |
| 280.700 | 0 | 0 | 232.000 | 232.000 |

## Interés por mora de un pago posterior al día 10

Al terminar el día 10 se congela la base imponible con todos los componentes
que estaban vencidos. Si todavía no corrió el job del día 11, un pago posterior
al corte calcula esa misma fotografía inmediatamente antes de registrar el
crédito:

```text
base imponible = alquiler vencido del mes + todos los intereses impagos al corte
interés por mora = redondear hacia abajo a centenas(base imponible * 10%)
```

Reglas que no deben confundirse:

1. El capital de alquileres anteriores se usa para imputar el pago, pero no se
   vuelve a sumar a la base imponible. Sobre ese capital ya se generó su mora.
2. Todos los intereses que estaban impagos al corte se suman a la base. Si el
   cliente los cancela con un pago posterior al día 10, la penalización no se
   reduce: esos intereses también fueron pagados fuera de término.
3. El alquiler vencido del mes queda como base aunque el pago tardío lo cancele,
   por la misma razón.
4. Un alquiler o interés cancelado hasta el día 10 no forma parte de la base.
5. La cascada sigue determinando qué deuda cancela el crédito y cómo queda el
   estado de cuenta, pero no reescribe la fotografía usada para la mora del mes.

Ejemplo con alquiler vencido de $232.000 e intereses previos de $48.700:

| Pago tardío | Capital anterior antes del pago | Intereses al corte | Base imponible | Nuevo interés |
|---:|---:|---:|---:|---:|
| 170.000 | 232.000 | 48.700 | 280.700 | 28.000 |
| 250.000 | 232.000 | 48.700 | 280.700 | 28.000 |
| 280.700 | 232.000 | 48.700 | 280.700 | 28.000 |

El modal de confirmación de Finanzas y Dashboard muestra la base congelada. El
importe del pago tardío no descuenta el alquiler ni los intereses de esa base,
aunque luego la cascada los cancele contablemente. El backend vuelve a calcular
la fotografía cuando todavía no hay un recargo pendiente; si el job del día 11
ya lo fijó, conserva ese importe como fuente de verdad. Si se elige **próximo
pago**, crea en el acto un débito fechado el día 1 del mes siguiente; no espera
al job mensual. Si se elige **cobrar ahora**, el débito se fecha el día del pago.
El importe mostrado y el débito deben coincidir, salvo que el usuario haya usado
explícitamente “Modificar monto”.

## Implementación y despliegue

1. Ejecutar `Database/Update_Add_PaymentComponentAllocations.sql` en la base
   correspondiente **antes de desplegar la API nueva**. El script es idempotente
   y transaccional; no inserta, elimina ni modifica movimientos o pagos.
2. Publicar el código nuevo de la API, pero mantener deshabilitado el ingreso de
   pagos hasta completar la reconstrucción.
3. Reconstruir los alquileres afectados desde `account_movements` con
   `ClientMonthBalanceService.RebuildForRentalAsync(rentalId)`. Para una puesta en
   marcha general, usar `RebuildAllActiveRentalsAsync()` una sola vez después de
   aplicar la migración y antes de habilitar nuevos pagos.
4. Publicar el frontend y habilitar nuevamente la API.
5. Verificar un pago parcial: capital anterior e intereses deben coincidir entre
   Clientes, detalle, Finanzas, estado de cuenta y snapshot para uso sin conexión.

Columnas agregadas a `client_month_balances`:

- `allocated_rent`: capital del período efectivamente cancelado.
- `allocated_interests`: intereses del período efectivamente cancelados.
- `unpaid_rent` y `unpaid_interests`: columnas calculadas con los saldos restantes.

Las columnas de imputación son nullable para distinguir registros antiguos. Las
columnas calculadas conservan la lectura anterior cuando la imputación es NULL:
la migración **no reinterpreta de golpe todos los pagos históricos**.
Cuando un alquiler se reconstruye desde `account_movements` (al registrar,
eliminar o ajustar movimientos), el motor reaplica sus créditos por fecha con la
nueva prioridad y persiste la separación explícita.

La API nueva no debe ejecutarse contra una base sin esta migración. Esta consulta
debe devolver cuatro filas antes del despliegue:

```sql
SELECT name
FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.client_month_balances')
  AND name IN ('allocated_rent', 'allocated_interests', 'unpaid_rent', 'unpaid_interests');
```

Si el pago del caso ya fue registrado en producción, aplicar solamente el cambio
de esquema no lo recalcula. Requiere reconstruir el alquiler desde sus movimientos
con `ClientMonthBalanceService.RebuildForRentalAsync(rentalId)` en el entorno de
destino. No registrar otra vez el pago para forzar el recálculo. Los conceptos de
pagos históricos ya emitidos no se reescriben por una reconstrucción de saldos.

Consulta de verificación (reemplazar el identificador; no ejecuta cambios):

```sql
DECLARE @RentalId INT = NULL; -- rental_id real, no payment_identifier
SELECT month_year, previous_balance, interests, monthly_debits,
       paid, advanced_payment, allocated_rent, allocated_interests,
       unpaid_rent, unpaid_interests,
       balance - paid - advanced_payment AS deuda_acumulada
FROM dbo.client_month_balances
WHERE rental_id = @RentalId
ORDER BY RIGHT(month_year, 4), LEFT(month_year, 2);
```

`previous_balance` interno sigue siendo el arrastre acumulado del mes anterior;
no es directamente la columna visual “Saldo anterior”, que suma capital anterior
pendiente y excluye intereses. No se debe inferir la parte cancelada de capital
restando intereses de `paid`: usar `allocated_rent` / `unpaid_rent`.

## Fecha de próximo pago

La última decisión válida guardada con el pago tiene prioridad. Permite mantener
un mes parcialmente pagado como pendiente o avanzar conservando el faltante
como deuda anterior. Ver "Consulta de meses parcialmente pagados y excedentes".
La fecha explícita puede ser anterior al mes actual; no limitarla automáticamente.

Sin decisión válida, se conserva el máximo entre el mes actual y el siguiente
al último alquiler con capital imputado (monthly_debits > 0 y
unpaid_rent < monthly_debits). La fecha formal de un pago no acredita un mes.
Los débitos planificados pueden tener payment_id NULL aunque estén cubiertos.
Esta regla histórica se mantiene para cuentas anteriores a la migración.

## Prevención de débitos mensuales duplicados

El job mensual, la planificación y la proyección automática de un pago pueden
intentar crear el mismo débito al mismo tiempo. Una consulta previa sin bloqueo
no alcanza: ambas transacciones podrían leer que todavía no existe y luego
insertar dos filas.

Todos esos caminos deben usar
`DaoAccountMovement.IsDebitAlreadyCreatedAsync`. El método obtiene un
`sp_getapplock` exclusivo, con duración de la transacción, para la combinación
de alquiler y concepto base del período antes de comprobar la existencia. El
bloqueo se conserva hasta confirmar o revertir la transacción, por lo que el
segundo proceso espera y luego observa el débito creado por el primero.

No se requiere una modificación de esquema para este control. Tampoco debe
resolverse con un modal: elegir si se proyecta un mes futuro puede ser una
decisión de negocio, pero crear dos cargos por el mismo alquiler y período es
siempre un error de integridad.

La corrección impide nuevas carreras, pero no elimina movimientos históricos.
Esta consulta de solo lectura permite detectar períodos que ya tengan más de un
débito de alquiler antes de decidir una corrección caso por caso:

```sql
SELECT
    rental_id,
    DATEFROMPARTS(YEAR(movement_date), MONTH(movement_date), 1) AS rent_period,
    COUNT(*) AS debit_count,
    SUM(amount) AS total_debited
FROM dbo.account_movements
WHERE movement_type = 'DEBITO'
  AND concept LIKE 'Alquiler %'
GROUP BY
    rental_id,
    DATEFROMPARTS(YEAR(movement_date), MONTH(movement_date), 1)
HAVING COUNT(*) > 1
ORDER BY rent_period, rental_id;
```

## Verificación reproducible

```powershell
dotnet build tests/PaymentWaterfall.Checks/PaymentWaterfall.Checks.csproj --artifacts-path .codex_tmp/payment-waterfall/artifacts -p:UseSharedCompilation=false --disable-build-servers
dotnet .codex_tmp/payment-waterfall/artifacts/bin/PaymentWaterfall.Checks/debug/PaymentWaterfall.Checks.dll
```

El ejecutable prueba el motor, la migración y el servicio real de pagos usando
autenticación integrada contra `.\SQLEXPRESS`. Crea una base temporal con nombre
único y la elimina al terminar, también si una prueba falla. No lee appsettings,
no usa bases de negocio y no inicia la API, Quartz ni servidores web.

## Consulta de meses parcialmente pagados y excedentes (2026-10-09)

La imputación contable sigue la cascada: capital anterior, intereses y alquileres.
La decisión del administrativo modifica el mes que se debe cobrar, sin perdonar
capital ni simular pagos adicionales.

- Un débito previamente generado que recibe capital y queda parcialmente pagado
  consulta si ese mes sigue pendiente o se toma como pagado para la cobranza.
  La primera opción mantiene ese mes como Próx. pago y no emite otro débito.
  La segunda pasa al mes siguiente, genera su débito si no existe y conserva
  lo que faltó pagar como Saldo anterior. Un débito ya planificado se reutiliza.
  No volver a consultar por capital de un mes anterior a una fecha ya elegida;
  un pago destinado sólo a esa deuda conserva el mes de cobranza todavía pendiente.
- Si se cancelan los débitos existentes y sobra dinero, se genera el próximo
  débito. Se consulta si ese mes con crédito sigue pendiente (credit_next_month)
  o se considera pagado para cobranza (close_credited_month). La primera opción
  conserva ese mes como Próx. pago; la segunda agrega un único débito adicional
  y lo fija como Próx. pago. El capital no cubierto del mes intermedio queda
  como Saldo anterior. No extender meses indefinidamente por el excedente.
- Ejemplo: abono 100, octubre pendiente 100 y pago 110. Mantener noviembre:
  Saldo anterior +10, Saldo -90, Próx. pago noviembre. Pasar a diciembre:
  Saldo anterior -90, Saldo -190, Próx. pago diciembre.
- Ejemplo: octubre pendiente 100 y pago 50. Mantener octubre: Saldo anterior 0,
  Saldo -50, Próx. pago octubre. Pasar a noviembre: Saldo anterior -50,
  Saldo -150, Próx. pago noviembre.
- Un pago habitual exacto genera solamente el próximo débito. Los adelantos
  mantienen exactamente los meses seleccionados, sin un mes adicional fuera
  del período. Si su importe no coincide, se conserva la consulta del adelanto.
- SE VA, precio congelado u omisión explícita de proyección mantienen sus
  restricciones. No ofrecer alquileres nuevos que esas reglas prohíben.
  Sin alquiler previo, se conserva la consulta para emitir el mes del pago.
  Intereses futuros no desplazan los períodos de alquiler.

El modal muestra Próx. pago a la derecha de cada opción, débitos a generar,
Saldo anterior y Saldo final con su signo. El saldo total conserva todos los
movimientos, incluso futuros ya planificados; Saldo anterior se calcula respecto
al mes elegido. La vista previa reproduce el mismo ledger que la confirmación,
incluyendo una sola vez las comisiones, bonificaciones y el recargo real del servidor.

PaymentProjectionPlanner calcula las opciones dentro de la transacción y del
bloqueo por cliente. HTTP 422 PAYMENT_DECISION_REQUIRED revierte todo el intento,
incluyendo pago, movimientos, aumentos y congelamientos. La aprobación liga
FutureDebitAction y PaymentDecisionToken al estado original y a los importes.
Una cuenta modificada se rechaza con HTTP 409; datos u opciones modificados
requieren otra consulta. Finanzas y Dashboard usan el mismo diálogo. Cancelar
conserva el formulario y no publica actualizaciones.

Aplicar Database/Update_Add_PaymentCollectionDecisions.sql antes de desplegar.
La tabla payment_collection_decisions guarda una decisión por pago, dentro de
la misma transacción. Una fila automática con fecha nula reemplaza decisiones
anteriores cuando dejan de corresponder. La última decisión válida tiene prioridad
sobre el cálculo histórico y NO se limita al mes actual: un mes anterior puede
seguir pendiente por decisión expresa. dbo.GetPaymentCollectionMonth y
GetPaymentCollectionPreviousBalance comparten esa lectura entre Clientes,
detalle, filtros, Finanzas, Dashboard, Caja, Comunicaciones y snapshot offline.
Reconstruir saldos no elimina la decisión. Borrar el pago elimina su decisión
por FK y recalcula; si se borra un pago posterior, puede volver a regir la
anterior. Un crédito manual posterior invalida la fecha anterior y recupera el
cálculo por imputaciones; los cambios de importes actualizan los saldos en cada
lectura. Sin una decisión válida, las cuentas históricas conservan su regla.
La migración no altera pagos, importes ni débitos históricos.

Los pagos offline que necesitan consulta quedan fallidos para revisión con
conexión, sin efectos en la cuenta; no decidir por el administrativo al sincronizar.
Database/Check_PaymentProjectionDecisions.sql contiene controles de sólo lectura.
Verificar comportamiento con SQL descartable PaymentDecisionChecks, además de
las pruebas del servicio y diálogo Angular. Compilar no demuestra esta lógica.
