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

-- Lockers
INSERT INTO lockers (warehouse_id, locker_type_id, identifier, features, status, active) VALUES
(1, 1, 'B-101', 'Baulera en planta baja, fácil acceso', 'DISPONIBLE', 1),
(1, 2, 'XL-202', 'Baulera grande con ventilación', 'OCUPADO', 1),
(2, 3, 'G-301', 'Baulera extra grande con estanterías', 'OCUPADO', 1),
(2, 4, 'B-102', 'Box en planta baja', 'DISPONIBLE', 1),
(3, 5, 'L-103', 'Locker con acceso digital', 'OCUPADO', 1),
(3, 6, 'E-104', 'Espacio libre para almacenamiento flexible', 'DISPONIBLE', 1),
(2, 3, 'G-305', 'Baulera extra grande', 'DISPONIBLE', 1);

-- Increase Regimens
INSERT INTO increase_regimens (frequency, percentage) VALUES
(6, 10.00),
(12, 20.00);

-- Clients
INSERT INTO clients (payment_identifier, first_name, last_name, registration_date, notes, dni, cuit, preferred_payment_method_id, active, iva_condition) VALUES
(0.01, 'Juan', 'Pérez', '2024-01-10', 'Cliente puntual', '30123456', '20-30123456-7', NULL, 1, 'Consumidor Final'),
(0.02, 'María', 'González', '2024-02-15', 'Solicitó espacio adicional', '28987654', '27-28987654-8', NULL, 1, 'Responsable Inscripto');

-- Clients x Regimens
INSERT INTO clients_x_increase_regimens (client_id, regimen_id, registration_date, end_date) VALUES
(1, 1, '2024-01-10', NULL),
(2, 2, '2024-02-15', NULL);

-- Phones
INSERT INTO phones (client_id, number, type, whatsapp, active) VALUES
(1, '1160244907', 'Móvil', 1, 1),
(1, '1160265907','Casa', 0, 1),
(2, '1145678901', 'Trabajo', 0, 1),
(2, '1161474907','Móvil', 1, 1);

-- Emails
INSERT INTO emails (client_id, address, type, active) VALUES
(1, 'juan.perez@mail.com', 'Personal', 1),
(2, 'maria.gonzalez@mail.com', 'Trabajo', 1);

-- Addresses
INSERT INTO addresses (client_id, street, city, province) VALUES
(1, 'Calle Falsa 123', 'Buenos Aires', 'Buenos Aires'),
(2, 'Av. Libertador 456', 'San Isidro', 'Buenos Aires');

-- Payment Methods
INSERT INTO payment_methods (name, active) VALUES
('Efectivo', 1),
('Transferencia Bancaria', 1),
('Tarjeta de Crédito', 1);

-- Rentals
INSERT INTO rentals (client_id, start_date, end_date, contracted_m3, active) VALUES
(1, '2024-01-15', NULL, 8.00, 1),
(2, '2024-02-20', NULL, 10.00, 1);

-- Rental Amount History
INSERT INTO rental_amount_history (rental_id, amount, start_date, end_date) VALUES
(1, 92000, '2024-01-15', NULL),
(2, 115000, '2024-02-20', NULL);

-- Payments
INSERT INTO payments (client_id, payment_method_id, payment_date, amount) VALUES
(1, 1, '2024-01-20', 92000),
(2, 2, '2024-02-25', 115000);

-- Account Movements
INSERT INTO account_movements (rental_id, movement_date, movement_type, concept, payment_id) VALUES
(1, '2024-01-20', 'CREDITO', 'Pago alquiler enero', 1),
(2, '2024-02-25', 'CREDITO', 'Pago alquiler febrero', 2);

-- User Types
INSERT INTO user_types (name, active) VALUES
('Administrador', 1),
('Empleado', 1);

-- Users
INSERT INTO users (user_type_id, username, first_name, last_name, password, active) VALUES
(1, 'chino', 'Augusto', 'Palastanga', 'claveSegura123', 1),
(2, 'robert', 'Roberto', 'Filgueira', 'claveRoberto456', 1);

-- Activity Log (JSON in old_value and new_value)
INSERT INTO activity_log (user_id, log_date, action, table_name, record_id, old_value, new_value) VALUES
(1, GETDATE(), 'UPDATE', 'clients', 1, '{"name":"Juan"}', '{"name":"Juan Pablo"}');
