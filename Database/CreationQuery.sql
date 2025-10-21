CREATE DATABASE GuardeSoftware;
GO

USE GuardeSoftware;
GO

BEGIN TRANSACTION;
GO

BEGIN TRY
-- Table: locker_types
CREATE TABLE locker_types (
    locker_type_id INT IDENTITY(1,1) PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    amount DECIMAL(10,2) NOT NULL,
    m3 DECIMAL(10,2),
    active BIT DEFAULT 1
);

-- Table: warehouses
CREATE TABLE warehouses (
    warehouse_id INT IDENTITY(1,1) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    address VARCHAR(255),
    active BIT DEFAULT 1
);

-- Table: increase_policies
CREATE TABLE increase_regimens (
    regimen_id INT IDENTITY(1,1) PRIMARY KEY,
    frequency INT NOT NULL,
    percentage DECIMAL(5,2) NOT NULL
);

-- Table: clients
CREATE TABLE clients (
    client_id INT IDENTITY(1,1) PRIMARY KEY,
    payment_identifier DECIMAL(10,2),
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    registration_date DATE NOT NULL,
    notes VARCHAR(MAX),
    dni VARCHAR(20),
    cuit VARCHAR(20),
    preferred_payment_method_id INT,
    iva_condition VARCHAR(50),
    active BIT DEFAULT 1
);

-- Table: clients_increase_policies
CREATE TABLE clients_x_increase_regimens (
    client_id INT,
    regimen_id INT,
    start_date DATE NOT NULL,
    end_date DATE,
    PRIMARY KEY (client_id, regimen_id),
    FOREIGN KEY (client_id) REFERENCES clients(client_id),
    FOREIGN KEY (regimen_id) REFERENCES increase_regimens(regimen_id)
);

-- Table: phones
CREATE TABLE phones (
    phone_id INT IDENTITY(1,1) PRIMARY KEY,
    client_id INT NOT NULL,
    number VARCHAR(20) NOT NULL,
    type VARCHAR(50),
    whatsapp BIT NOT NULL,
    active BIT DEFAULT 1,
    FOREIGN KEY (client_id) REFERENCES clients(client_id)
);

-- Table: emails
CREATE TABLE emails (
    email_id INT IDENTITY(1,1) PRIMARY KEY,
    client_id INT NOT NULL,
    address VARCHAR(150) NOT NULL,
    type VARCHAR(50),
    active BIT DEFAULT 1
    FOREIGN KEY (client_id) REFERENCES clients(client_id)
);

-- Table: addresses
CREATE TABLE addresses (
    address_id INT IDENTITY(1,1) PRIMARY KEY,
    client_id INT NOT NULL,
    street VARCHAR(255) NOT NULL,
    city VARCHAR(100) NOT NULL,
    province VARCHAR(100),
    FOREIGN KEY (client_id) REFERENCES clients(client_id)
);

-- Table: payment_methods
CREATE TABLE payment_methods (
    payment_method_id INT IDENTITY(1,1) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    commission DECIMAL(5,2),
    active BIT DEFAULT 1
);

-- Table: rentals
CREATE TABLE rentals (
    rental_id INT IDENTITY(1,1) PRIMARY KEY,
    client_id INT NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE,
    contracted_m3 DECIMAL(10,2),
    months_unpaid INT DEFAULT 0,
    active BIT DEFAULT 1, -- We can use end_date to determine if the rental is active, but this can be useful for quick checks & performance
    FOREIGN KEY (client_id) REFERENCES clients(client_id)
);

CREATE TABLE lockers (
    locker_id INT IDENTITY(1,1) PRIMARY KEY,
    warehouse_id INT NOT NULL,
    locker_type_id INT NOT NULL,
    identifier VARCHAR(100) UNIQUE,
    features VARCHAR(MAX),
    status VARCHAR(50) NOT NULL,
    rental_id INT,
    active BIT DEFAULT 1,
    FOREIGN KEY (warehouse_id) REFERENCES warehouses(warehouse_id),
    FOREIGN KEY (locker_type_id) REFERENCES locker_types(locker_type_id),
    FOREIGN KEY (rental_id) REFERENCES rentals(rental_id)
);

-- Table: rental_amount_history
CREATE TABLE rental_amount_history (
    rental_amount_history_id INT IDENTITY(1,1) PRIMARY KEY,
    rental_id INT NOT NULL,
    amount DECIMAL(10,2) NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE,
    FOREIGN KEY (rental_id) REFERENCES rentals(rental_id)
);

-- Table: payments
CREATE TABLE payments (
    payment_id INT IDENTITY(1,1) PRIMARY KEY,
    client_id INT NOT NULL,
    payment_method_id INT NOT NULL,
    payment_date DATETIME NOT NULL,
    amount DECIMAL(10,2) NOT NULL,
    FOREIGN KEY (client_id) REFERENCES clients(client_id),
    FOREIGN KEY (payment_method_id) REFERENCES payment_methods(payment_method_id)
);

-- Table: account_movements
CREATE TABLE account_movements (
    movement_id INT IDENTITY(1,1) PRIMARY KEY,
    rental_id INT NOT NULL,
    movement_date DATE NOT NULL,
    movement_type VARCHAR(10) CHECK (movement_type IN ('DEBITO','CREDITO')) NOT NULL,
    concept VARCHAR(255),
    amount DECIMAL(10,2) NOT NULL,
    payment_id INT,
    FOREIGN KEY (rental_id) REFERENCES rentals(rental_id),
    FOREIGN KEY (payment_id) REFERENCES payments(payment_id)
);

-- Table: user_types
CREATE TABLE user_types (
    user_type_id INT IDENTITY(1,1) PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    active BIT DEFAULT 1
);

-- Table: users
CREATE TABLE users (
    user_id INT IDENTITY(1,1) PRIMARY KEY,
    user_type_id INT NOT NULL,
    username VARCHAR(100) NOT NULL,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    password VARCHAR(255),
    active BIT DEFAULT 1,
    FOREIGN KEY (user_type_id) REFERENCES user_types(user_type_id)
);

-- Table: activity_log
CREATE TABLE activity_log (
    activity_log_id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT NOT NULL,
    log_date DATETIME NOT NULL,
    action VARCHAR(50) NOT NULL,
    table_name VARCHAR(100) NOT NULL,
    record_id INT NOT NULL,
    old_value NVARCHAR(MAX),
    new_value NVARCHAR(MAX),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);

-- Communication channels (Email, WhatsApp, etc.)
CREATE TABLE communication_channels (
    channel_id INT PRIMARY KEY IDENTITY(1,1),
    name VARCHAR(50) NOT NULL UNIQUE,
    is_active BIT NOT NULL DEFAULT 1
);

-- The main communication or "campaign"
CREATE TABLE communications (
    communication_id INT PRIMARY KEY IDENTITY(1,1),
    creator_user_id INT NOT NULL, -- FK to your Users table
    title VARCHAR(255) NOT NULL,
    creation_date DATETIME NOT NULL DEFAULT GETDATE(),
    scheduled_date DATETIME NULL,
    -- 'Draft', 'Scheduled', 'Processing', 'Finished'
    status VARCHAR(30) NOT NULL DEFAULT 'Draft',
    FOREIGN KEY (creator_user_id) REFERENCES users(user_id)
);

-- Defines the specific content for each channel within a communication
CREATE TABLE communication_channel_content (
    comm_channel_content_id INT PRIMARY KEY IDENTITY(1,1),
    communication_id INT NOT NULL,
    channel_id INT NOT NULL,
    subject VARCHAR(255), -- Used mainly for email
    content NVARCHAR(MAX) NOT NULL, -- HTML for email, text for WhatsApp
    attachments NVARCHAR(MAX), -- Store a JSON array of file paths
    FOREIGN KEY (communication_id) REFERENCES communications(communication_id),
    FOREIGN KEY (channel_id) REFERENCES communication_channels(channel_id)
);

-- NEW TABLE: Defines which clients will receive the communication
CREATE TABLE communication_recipients (
    communication_recipient_id INT PRIMARY KEY IDENTITY(1,1),
    communication_id INT NOT NULL,
    client_id INT NOT NULL,
    FOREIGN KEY (communication_id) REFERENCES communications(communication_id),
    FOREIGN KEY (client_id) REFERENCES clients(client_id)
);

-- Logs every send attempt to a client via a specific channel
CREATE TABLE dispatches (
    dispatch_id BIGINT PRIMARY KEY IDENTITY(1,1),
    comm_channel_content_id INT NOT NULL, -- FK to communication_channel_content
    client_id INT NOT NULL,
    dispatch_date DATETIME,
    -- 'Pending', 'In Progress', 'Successful', 'Failed'
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    provider_response NVARCHAR(500), -- To save the MessageID or error
    FOREIGN KEY (comm_channel_content_id) REFERENCES communication_channel_content(comm_channel_content_id),
    FOREIGN KEY (client_id) REFERENCES clients(client_id)
);

COMMIT TRANSACTION;
    PRINT '¡Database and tables created successfully!';

END TRY
BEGIN CATCH


    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END

    PRINT 'Error: Could not create tables. All changes have been rolled back. Database can be deleted';

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR (@ErrorMessage, 16, 1);
END CATCH;
GO