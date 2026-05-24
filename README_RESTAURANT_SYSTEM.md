# Restaurant Ordering System - User Guide

> [!NOTE]
> didn't create migration or database

## New Smart Features (Added)

### 1. Table Sessions & Order Merging
- **Automated Sessions**: A session starts when a customer scans the QR and enters their name/phone.
- **Order Merging**: Customers can add items to an existing order. The system automatically merges new items into the active session's pending order or creates a linked order.
- **Manual Session Closure**: Waiters can close a table session manually from their dashboard when the table is cleared.

### 2. Smart Recommendations & Add-Ons
- **Upselling**: Admin can link "Add-Ons" (e.g., Extra Cheese) and "Recommendations" (e.g., Fries with Burger) to menu items.
- **Badges**: Items can be marked as "Best Seller", "Trending", or "Recommended" to help customers choose faster.

### 3. Customer Order Tracking
- **No Account Required**: Customers can track their order status in real-time by searching with their phone number.
- **Status Lifecycle**: Pending → In Preparation → Ready → Served → Paid.

### 4. Multi-Language Support
- Full support for **Arabic** and **English** in the customer UI.

## Quick Start

### 1. Database Setup
Run the following commands to set up the database (Note: Migrations for new features need to be created):
```bash
cd Resturant.Web.UI
dotnet ef migrations add AddSmartRestaurantFeatures
dotnet ef database update
```

### 2. Configure Cloudinary
Add your Cloudinary credentials to `appsettings.json`:
```json
{
  "CloudinarySettings": {
    "CloudName": "your-cloud-name",
    "ApiKey": "your-api-key",
    "ApiSecret": "your-api-secret"
  }
}
```

### 3. Run the Application
```bash
cd Resturant.Web.UI
dotnet run
```

... (rest of the content)


## User Roles

### Default Admin Account
- **Email**: admin@resturant.com
- **Password**: Admin@123

## How to Use

### For Restaurant Staff

#### 1. Create Tables and Generate QR Codes
1. Login as Admin
2. Navigate to `/Tables`
3. Click "Create New Table"
4. Enter table number (optional)
5. Enter website URL (optional, defaults to current domain)
6. Click "Create"
7. QR code will be generated and uploaded to Cloudinary
8. Print the QR code and place it on the table

#### 2. Manage Menu Categories
1. Login as Admin or Manager
2. Navigate to `/MenuCategory`
3. Create, edit, or delete menu categories

#### 3. Manage Menu Items
1. Login as Admin or Manager
2. Navigate to `/MenuItem`
3. Create new menu items:
   - Name
   - Description
   - Price
   - Category
   - Image (optional, uploads to Cloudinary)
   - Availability status

### For Customers

#### 1. Scan QR Code
1. Use your phone's camera or QR scanner app
2. Scan the QR code on your table
3. You'll be redirected to the restaurant's menu page

#### 2. Browse Menu
1. Select a category from the sidebar
2. View available items in that category
3. Click "Add to Cart" to add items

#### 3. Manage Cart
1. View your cart on the right side
2. Adjust quantities using + and - buttons
3. Remove items if needed
4. See total amount

#### 4. Place Order
1. Click "Checkout" button
2. If table number wasn't in QR code, enter it when prompted
3. Order is sent to the waiter dashboard

### For Waiters

#### 1. Access Dashboard
1. Login as Waiter
2. Navigate to `/Waiter`

#### 2. View Pending Orders
1. See all pending orders in real-time
2. Each order shows:
   - Order number
   - Table number
   - Order time
   - Items and quantities
   - Total amount

#### 3. Confirm Orders
1. Review order details
2. Verify table number
3. Click "Confirm Order"
4. Order moves to chef dashboard

### For Chefs

#### 1. Access Dashboard
1. Login as Chef
2. Navigate to `/Chef`

#### 2. View Orders to Prepare
1. See confirmed orders (Ready to Prepare)
2. See in-preparation orders (In Preparation)

#### 3. Prepare Orders
1. Click "Start Preparation" to begin
2. Order status changes to "In Preparation"
3. Click "Mark as Ready" when complete
4. Order moves to cashier dashboard

### For Cashiers

#### 1. Access Dashboard
1. Login as Cashier
2. Navigate to `/Cashier`

#### 2. View Ready Orders
1. See all completed orders ready for payment
2. Each order shows:
   - Order number
   - Table number
   - Order time
   - Items and quantities
   - Total amount

#### 3. Process Payment
1. Click "Process Payment"
2. Order is marked as completed
3. Click "Print Receipt" to generate receipt

## Order Flow

```
Customer → Places Order
    ↓
Waiter → Confirms Order
    ↓
Chef → Starts Preparation
    ↓
Chef → Marks as Ready
    ↓
Cashier → Processes Payment
    ↓
Order Completed
```

## Real-Time Updates

All dashboards update in real-time using SignalR:
- When customer places order → Waiter dashboard updates
- When waiter confirms order → Chef dashboard updates
- When chef marks ready → Cashier dashboard updates
- When cashier processes payment → Order is completed

## Table Number Storage

- Table numbers are stored in browser local storage
- Automatically expires after 1 hour
- Cleared when order is completed

## QR Code Format

QR codes contain:
```
https://your-restaurant.com/Menu?tableNumber=5
```

If table number is null:
```
https://your-restaurant.com/Menu
```

## Troubleshooting

### Orders not appearing in dashboards
1. Check SignalR connection in browser console
2. Verify user has correct role
3. Check order status in database

### QR codes not generating
1. Verify Cloudinary credentials in appsettings.json
2. Check internet connection
3. Verify Cloudinary account has sufficient quota

### Cart not persisting
1. Check browser local storage is enabled
2. Clear browser cache and try again

## API Endpoints

### Menu
- `GET /Menu` - Browse menu
- `GET /Menu/GetMenuItemsByCategory?categoryId=X` - Get items by category
- `GET /Menu/GetAllCategories` - Get all categories

### Orders
- `POST /Order/Create` - Create new order
- `GET /Order/GetOrdersByStatus?status=X` - Get orders by status
- `POST /Order/UpdateStatus` - Update order status
- `GET /Order/GetOrderDetails?id=X` - Get order details

## Order Status Values

| Status | Value | Description |
|--------|--------|-------------|
| Pending | 0 | Customer placed order |
| Confirmed | 1 | Waiter confirmed order |
| InPreparation | 2 | Chef is preparing |
| Ready | 3 | Chef completed preparation |
| Served | 4 | Order served to customer |
| Completed | 5 | Payment processed |
| Cancelled | 6 | Order cancelled |

## Security

- All dashboards require authentication
- Role-based access control
- Admin users have full access
- Manager users have full access
- Waiter, Chef, and Cashier have limited access to their respective dashboards

## Support

For issues or questions, contact the system administrator.
