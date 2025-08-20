CREATE DATABASE GuardeSoftware;
GO

USE GuardeSoftware;
GO

-- Table: locker_types
CREATE TABLE locker_types (
    locker_type_id INT IDENTITY(1,1) PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    amount DECIMAL(10,2) NOT NULL,
    cubic_meters DECIMAL(10,2),
    active BIT DEFAULT 1
);

-- Table: warehouses
CREATE TABLE warehouses (
    warehouse_id INT IDENTITY(1,1) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    address VARCHAR(255),
    active BIT DEFAULT 1
);

-- Table: lockers
CREATE TABLE lockers (
    locker_id INT IDENTITY(1,1) PRIMARY KEY,
    warehouse_id INT NOT NULL,
    locker_type_id INT NOT NULL,
    identifier VARCHAR(100) UNIQUE,
    features VARCHAR(MAX),
    status VARCHAR(50),
    FOREIGN KEY (warehouse_id) REFERENCES warehouses(warehouse_id),
    FOREIGN KEY (locker_type_id) REFERENCES locker_types(locker_type_id)
);

-- Table: increase_policies
CREATE TABLE increase_policies (
    policy_id INT IDENTITY(1,1) PRIMARY KEY,
    frequency INT NOT NULL,
    percentage DECIMAL(5,2) NOT NULL
);

-- Table: customers
CREATE TABLE customers (
    customer_id INT IDENTITY(1,1) PRIMARY KEY,
    payment_identifier DECIMAL(10,2),
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    registration_date DATE,
    notes VARCHAR(MAX),
    document_number VARCHAR(20),
    tax_id VARCHAR(20),
    preferred_payment_method_id INT,
    active BIT DEFAULT 1,
    tax_condition VARCHAR(50)
);

-- Table: customers_increase_policies
CREATE TABLE customers_increase_policies (
    customer_id INT,
    policy_id INT,
    start_date DATE NOT NULL,
    end_date DATE,
    PRIMARY KEY (customer_id, policy_id),
    FOREIGN KEY (customer_id) REFERENCES customers(customer_id),
    FOREIGN KEY (policy_id) REFERENCES increase_policies(policy_id)
);

-- Table: phones
CREATE TABLE phones (
    phone_id INT IDENTITY(1,1) PRIMARY KEY,
    customer_id INT NOT NULL,
    type VARCHAR(50),
    whatsapp BIT DEFAULT 0,
    FOREIGN KEY (customer_id) REFERENCES customers(customer_id)
);

-- Table: emails
CREATE TABLE emails (
    email_id INT IDENTITY(1,1) PRIMARY KEY,
    customer_id INT NOT NULL,
    email VARCHAR(150) NOT NULL,
    type VARCHAR(50),
    FOREIGN KEY (customer_id) REFERENCES customers(customer_id)
);

-- Table: addresses
CREATE TABLE addresses (
    address_id INT IDENTITY(1,1) PRIMARY KEY,
    customer_id INT NOT NULL,
    address VARCHAR(255) NOT NULL,
    city VARCHAR(100),
    state VARCHAR(100),
    FOREIGN KEY (customer_id) REFERENCES customers(customer_id)
);

-- Table: payment_methods
CREATE TABLE payment_methods (
    payment_method_id INT IDENTITY(1,1) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    active BIT DEFAULT 1
);

-- Table: rentals
CREATE TABLE rentals (
    rental_id INT IDENTITY(1,1) PRIMARY KEY,
    customer_id INT NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE,
    contracted_square_meters DECIMAL(10,2),
    active BIT DEFAULT 1,
    FOREIGN KEY (customer_id) REFERENCES customers(customer_id)
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
    customer_id INT NOT NULL,
    payment_method_id INT NOT NULL,
    payment_date DATE NOT NULL,
    amount DECIMAL(10,2) NOT NULL,
    FOREIGN KEY (customer_id) REFERENCES customers(customer_id),
    FOREIGN KEY (payment_method_id) REFERENCES payment_methods(payment_method_id)
);

-- Table: account_movements
CREATE TABLE account_movements (
    movement_id INT IDENTITY(1,1) PRIMARY KEY,
    rental_id INT NOT NULL,
    movement_date DATE NOT NULL,
    movement_type VARCHAR(10) CHECK (movement_type IN ('DEBIT','CREDIT')),
    concept VARCHAR(255),
    payment_id INT
    FOREIGN KEY (rental_id) REFERENCES rentals(rental_id),
    FOREIGN KEY (payment_id) REFERENCES payments(payment_id)
);

-- Table: user_types
CREATE TABLE user_types (
    user_type_id INT IDENTITY(1,1) PRIMARY KEY,
    name VARCHAR(50) NOT NULL
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
    action VARCHAR(50),
    table_name VARCHAR(100),
    record_id INT,
    old_value NVARCHAR(MAX),
    new_value NVARCHAR(MAX),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
