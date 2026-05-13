# Authentication Modal - Updates & Fixes

## ✅ Issues Fixed

### 1. **Changed Email to Username**
   - Updated both Login and Register forms to use "Username" instead of "Email"
   - Changed field names:
	 - `txtLoginEmail` → `txtLoginUsername`
	 - `txtRegisterEmail` → `txtRegisterUsername`
   - Updated validation messages accordingly

### 2. **Fixed TextBox Input Issue**
   - **Problem:** Custom control template was preventing text entry
   - **Root Cause:** Improper padding configuration and ScrollViewer configuration
   - **Solution:** 
	 - Added `VerticalContentAlignment="Center"` to TextBox and PasswordBox
	 - Fixed ScrollViewer by setting `Focusable="false"` and moving padding to ScrollViewer
	 - Removed padding from Border (was causing input blocking)
	 - Added proper visibility settings for ScrollBars
   - **Result:** TextBoxes and PasswordBoxes now accept input correctly

### 3. **Account Persistence - YES, Accounts Are Saved! 📁**
   - **Answer:** YES! Registered accounts are now permanently saved
   - **How it works:**
	 1. Users register → account saved to `%AppData%\Signify\users.json`
	 2. Close the app → accounts stay saved
	 3. Open app again → can log in with previously registered accounts

   - **Location:** Accounts stored in: `C:\Users\[YourUsername]\AppData\Roaming\Signify\users.json`
   - **Security:** Passwords are hashed with SHA256 (never stored in plain text)

## 📁 New Files Created

### `SignifyUI/Models/User.cs`
- User data model with Username, PasswordHash, and CreatedAt timestamp

### `SignifyUI/Services/AuthService.cs`
- `RegisterUser(username, password)` - Creates new account
- `LoginUser(username, password)` - Authenticates existing account
- Password hashing with SHA256
- File-based persistence with JSON storage
- Validation rules:
  - Username must be unique
  - Username minimum 3 characters
  - Password minimum 6 characters

## 🔄 Updated Files

### `SignifyUI/AuthenticationModal.xaml`
- Fixed TextBox and PasswordBox styles
- Changed all "Email" labels to "Username"
- Improved control template rendering

### `SignifyUI/AuthenticationModal.xaml.cs`
- Integrated `AuthService` for login/registration
- Added proper validation using the service
- Auto-clear fields after successful auth
- Dismiss modal on successful login
- Switch to login form after successful registration

## 🧪 Testing the Features

1. **Test TextBox Input:**
   - Click the profile button
   - Try typing in the Username and Password fields
   - Should work smoothly now

2. **Test Registration:**
   - Register with: `testuser` / `password123`
   - Close the app completely
   - Reopen the app

3. **Test Login with Saved Account:**
   - Click profile button
   - Enter: `testuser` / `password123`
   - Should log in successfully

4. **Test Duplicate Prevention:**
   - Try registering with same username again
   - Should show error: "Username already exists"

## 🛡️ Security Notes

- Passwords are never stored in plain text
- Each password is hashed with a salt before storage
- User data stored in user's AppData folder (Windows permission protected)
- All validation happens locally

## 📝 To-Do (Optional Enhancements)

- Add "Remember Me" checkbox
- Implement password reset functionality
- Add email verification
- Improve password strength requirements
- Add account deletion option
