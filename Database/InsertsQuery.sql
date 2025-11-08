BEGIN TRANSACTION;
GO

BEGIN TRY
-- Locker Types
INSERT INTO locker_types (name, amount, m3, active) VALUES
('BAULERA ESTÁNDAR', 92000, 8.00, 1),
('BAULERA XL', 115000, 10.00, 1),
('BAULERA GRANDE', 161000, 14.00, 1),
('BOX', 69000, 6.00, 1),
('LOCKER', 55000, 4.00, 1),
('ESPACIO LIBRE', 58000, 5.00, 1);

-- Deposits
INSERT INTO warehouses (name, address, active) VALUES
('Borges', 'Francisco Borges 4280, Buenos Aires', 1),
('Libertad', 'Libertad 4764', 1),
('Guemes', 'Guemes 3670', 1);

-- Increase Regimens
INSERT INTO increase_regimens (frequency, percentage) VALUES
(4, 10.00),
(4, 15.00),
(4, 20.00),
(4, 40.00);

-- Payment Methods
INSERT INTO payment_methods (name, active, commission) VALUES
('Efectivo', 1, 0.00),
('Transferencia Bancaria', 1, 24.00);

-- Billing Types
INSERT INTO billing_types (name) VALUES 
('Factura A'),
('Factura B'),
('Factura C'),
('Factura A (No enviar)'),
('Factura B (No enviar)'),
('Factura C (No enviar)'),
('Sin Factura');

-- Clients
INSERT INTO clients (payment_identifier, first_name, last_name, registration_date, notes, dni, cuit, preferred_payment_method_id, active, iva_condition) VALUES
(0.01, 'Carmen', 'Veghitti ', '2012-04-28', 'Perfumeria Liliana Fredy, PAGO DESDE MAYO 2025 HASTA MAYO 2026', '', '', 1, 1, NULL),
(0.03, 'Susana', 'Galante', '2012-10-20', '', '10508492', '27105084922', 2, 1, 'Consumidor Final'),
(0.04, 'Maria Jose', 'Battafarano', '2013-02-02', '', '18084787', '27180847877', 2, 1, 'Consumidor Final Factura'),
(0.05, 'Mariano', 'Sturmer', '2013-02-22', '', '27768913', NULL, 1, 1, 'Consumidor Final Factura'),
(0.06, 'Ellison', 'Craig', '2013-03-19', 'DEPOSITA POR CAJERO LACROZE, Bravo Paula', '95052439', '20062358484', 2, 1, 'Consumidor Final'),
(0.09, 'Martin', 'Rodriguez', '2013-09-03', '', '25182349', '23251823499', 2, 1, 'Consumidor Final'),
(0.10, 'Alicia', 'Rodriguez', '2013-09-26', '', '13145717', '27131457176', 2, 1, 'Consumidor Final'),
(0.11, 'Fabian Eduardo', 'Chedrese', '2013-10-22', 'PAGO DESDE SEPTIEMBRE 2024 HASTA AGOSTO 2025 INCLUSIVE', '17538603', '20175386034', 1, 1, NULL),
(0.12, 'Eduardo', 'Palomero', '2013-10-25', 'Tiene alquiler en dos depositos', '7657286', '20076572861', 2, 1, 'Responsable Inscripto');

-- Clients x Regimens
INSERT INTO clients_x_increase_regimens (client_id, regimen_id, start_date, end_date) VALUES
(3, 2, '2025-09-01', NULL),
(4, 3, '2025-09-01', NULL),
(5, 2, '2025-09-01', NULL),
(6, 2, '2025-09-01', NULL),
(8, 4, '2025-09-01', NULL),
(9, 2, '2025-09-01', NULL);

-- Phones
INSERT INTO phones (client_id, number, type, whatsapp, active) VALUES
(1, '47492670', NULL, 0, 1),
(2, '1540898915', 'Móvil', 1, 1),
(3, '1540860883','Móvil', 1, 1),
(3, '48226164', NULL, 0, 1),
(4, '1535920505','Móvil', 1, 1),
(4, '47941628', NULL, 0, 1),
(5, '47864618', NULL, 0, 1),
(6, '47605741', NULL, 0, 1),
(7, '1540709430','Móvil', 1, 1),
(8, '47426033', NULL, 0, 1),
(8, '1544066676','Móvil', 1, 1),
(9, '1552488655', 'Móvil', 1, 1),
(9, '43716464', NULL, 0, 1),
(9, '43710431', NULL, 0, 1);

-- Emails
INSERT INTO emails (client_id, address, type, active) VALUES
(1, 'silto72@hotmail.com', NULL, 1),
(2, 'telecenter2002@hotmail.com', NULL, 1),
(3, 'mjbattafarano@me.com', NULL, 1),
(3, 'mjbattafarano@yahoo.com.ar', NULL, 1),
(4, 'msturmer@sinectis.com.ar', NULL, 1),
(5, 'manubravo36@hotmail.com', NULL, 1),
(6, 'martinalejandrorodriguez201@gmail.com', NULL, 1),
(7, 'aliziar32@gmail.com', NULL, 1),
(8, 'info@ecoproductos.com.ar', NULL, 1),
(9, 'ealp@fibertel.com.ar', NULL, 1),
(9, 'estudiolpvt@fibertel.com.ar', NULL, 1);

-- Addresses
INSERT INTO addresses (client_id, street, city, province) VALUES
(1, 'Vacio', 'Tigre', 'Buenos Aires'),
(2, 'Independencia 1020', 'Lobos', 'Buenos Aires'),
(3, 'Azcuenaga 884', 'Vicente Lopez', 'Buenos Aires'),
(4, 'Acassuso 2689', 'Olivos', 'Buenos Aires'),
(5, 'Aguilar 2476 6ª A', 'CABA', 'CABA'),
(6, 'Peru 1241 Dto 5', 'Florida', 'Buenos Aires'),
(7, 'Carlos Tejedor 3216', 'Carapachay', 'Buenos Aires'),
(8, 'Laprida 311', 'San Isidro', 'Buenos Aires'),
(9, 'Pte Quintana 260 6to P B', 'CABA', 'CABA');

-- Rentals
INSERT INTO rentals (client_id, start_date, end_date, contracted_m3, months_unpaid, active) VALUES
(1, '2012-04-28', NULL, NULL, 0, 1),
(2, '2012-10-20', NULL, 6, 0, 1),
(3, '2013-02-02', NULL, 4, 0, 1),
(4, '2013-02-22', NULL, 8, 0, 1),
(5, '2013-03-19', NULL, 8, 0, 1),
(6, '2013-09-03', NULL, 4, 0, 1),
(7, '2013-09-26', NULL, 8, 0, 1),
(8, '2013-10-22', NULL, NULL, 0, 1),
(9, '2013-10-25', NULL, 4, 0, 1);

-- Lockers
INSERT INTO lockers (warehouse_id, locker_type_id, identifier, features, status, rental_id, active) VALUES
(1, 4, 'OH', 'OHIGGIGUINS TIPO? ', 'OCUPADO', 1, 1),
(1, 4, 'PA-B', 'Planta alta borgues TIPO?', 'OCUPADO', 1, 1),
(1, 4, 'B-302', 'BOX 302', 'OCUPADO', 2, 1),
(3, 5, 'G-96', '96 TIPO?', 'OCUPADO', 3, 1),
(3, 5, 'G-99', '99 TIPO?', 'OCUPADO', 3, 1),
(3, 4, 'G-45', '45 TIPO?', 'OCUPADO', 4, 1),
(3, 1, 'G-83', '83 TIPO?', 'OCUPADO', 5, 1),
(3, 5, 'G-61', '61 TIPO?', 'OCUPADO', 6, 1),
(1, 5, 'B-202', '202 TIPO?', 'OCUPADO', 7, 1),
(1, 5, 'B-PS', 'Primer Salon TIPO?', 'OCUPADO', 7, 1),
(1, 2, 'B-PA', 'PASILLO BORGUES TIPO?', 'OCUPADO', 8, 1),
(1, 2, 'B-OFI1', 'OFI TIPO?', 'OCUPADO', 8, 1),
(3, 1, 'G-64', '64 TIPO?', 'OCUPADO', 9, 1),
(3, 1, 'G-67', '67 TIPO?', 'OCUPADO', 9, 1),
(1, 6, 'EL', 'ESPACIO LIBRE', 'OCUPADO', 9, 1),

(1, 1, 'T-1', 'Baulera de test', 'DISPONIBLE', NULL, 1),
(1, 1, 'T-2', 'Baulera de test', 'DISPONIBLE', NULL, 1),
(1, 2, 'T-3', 'Baulera de test', 'DISPONIBLE', NULL, 1),
(3, 2, 'T-4', 'Baulera de test', 'DISPONIBLE', NULL, 1),
(1, 2, 'T-5', 'Baulera de test', 'DISPONIBLE', NULL, 1),
(1, 2, 'T-6', 'Baulera de test', 'DISPONIBLE', NULL, 1),
(2, 3, 'T-7', 'Baulera de test', 'DISPONIBLE', NULL, 1),
(2, 3, 'T-8', 'Baulera de test', 'DISPONIBLE', NULL, 1),
(1, 4, 'T-9', 'Baulera de test', 'DISPONIBLE', NULL, 1),
(3, 4, 'T-10', 'Baulera de test', 'DISPONIBLE', NULL, 1),
(2, 5, 'T-11', 'Baulera de test', 'DISPONIBLE', NULL, 1),
(3, 6, 'T-12', 'Baulera de test', 'DISPONIBLE', NULL, 1);

-- Rental Amount History
INSERT INTO rental_amount_history (rental_id, amount, start_date, end_date) VALUES
(1, 60000, '2025-09-01', NULL),
(2, 31900, '2025-09-01', NULL),
(3, 94000, '2025-09-01', NULL),
(4, 84300, '2025-09-01', NULL),
(5, 91900, '2025-09-01', NULL),
(6, 62300, '2025-09-01', NULL),
(7, 109100, '2025-09-01', NULL),
(8, 237300, '2025-09-01', NULL),
(9, 278600, '2025-09-01', NULL);

INSERT INTO communication_channels (name) VALUES ('Email'), ('WhatsApp');

-- -- Payments
-- INSERT INTO payments (client_id, payment_method_id, payment_date, amount) VALUES
-- (1, 1, '2024-01-20', 92000),
-- (2, 2, '2024-02-25', 115000);

-- -- Account Movements
-- INSERT INTO account_movements (rental_id, movement_date, movement_type, concept, amount, payment_id) VALUES
-- (1, '2024-01-20', 'CREDITO', 'Pago alquiler enero', 60000, 1),
-- (2, '2024-02-25', 'CREDITO', 'Pago alquiler febrero', 15000, 2);

-- User Types
INSERT INTO user_types (name, active) VALUES
('Administrador', 1),
('Empleado', 1);

-- Users
INSERT INTO users (user_type_id, username, first_name, last_name, password, active) VALUES
(2, 'chino', 'Augusto', 'Palastanga', 'claveSegura123', 1),
(1, 'robert', 'Roberto', 'Filgueira', 'claveRoberto456', 1);

-- Activity Log (JSON in old_value and new_value)
-- INSERT INTO activity_log (user_id, log_date, action, table_name, record_id, old_value, new_value) VALUES
-- (1, GETDATE(), 'UPDATE', 'clients', 1, '{"name":"Juan"}', '{"name":"Juan Pablo"}');

COMMIT TRANSACTION;
    PRINT '¡Data inserted successfully!';

END TRY
BEGIN CATCH


    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END

    PRINT 'Error: Could not insert the data. All changes have been rolled back.';

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR (@ErrorMessage, 16, 1);
END CATCH;
GO