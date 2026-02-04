# UI Development Guide - Mock Mode Enabled

## ✅ Mock Mode is Now Active

Your frontend is now configured to work **without the backend API** during development. This means you can redesign and improve the UI while the database is being rebuilt on Supabase.

---

## How It Works

### Automatic Mock Mode
- **Development environment**: Mock data is automatically enabled
- **Production/other environments**: Real API calls are used

The toggle is controlled by `UseMockApi` in:
- `SchoolPortal.Client/wwwroot/appsettings.Development.json` → `"UseMockApi": true`

### What's Available
All major services now have mock implementations with realistic fake data:

- ✅ **Announcements** - 5 sample announcements
- ✅ **Classes** - 3 sample classes with students/teachers
- ✅ **Subjects** - 5 subjects (Math, Physics, Chemistry, etc.)
- ✅ **Users** - Admin and Teacher sample users
- ✅ **Auth** - Mock login (any email/password works)
- ✅ **Submissions, Grades, Attendance** - Basic mock data

---

## How to Use Mock Mode

### 1. Run the Frontend Only
```powershell
cd SchoolPortal.Client
dotnet run
```

No need to start the backend server!

### 2. Login
Use any credentials (mock auth accepts anything):
- Email: `demo@school.com`
- Password: `anything`

### 3. Navigate Pages
All pages that use the mocked services will work:
- `/announcements` ✅
- `/classes` ✅
- `/subjects` ✅
- `/users` ✅

---

## Switching Between Mock and Real API

### Use Mock Data (Development)
Edit `appsettings.Development.json`:
```json
{
  "UseMockApi": true
}
```

### Use Real API
Edit `appsettings.Development.json`:
```json
{
  "UseMockApi": false,
  "ApiBaseUrl": "http://localhost:5128"
}
```

---

## UI Improvement Workflow

Now that you can work independently, here's a suggested workflow:

### Phase 1: Visual Polish (Current Focus)
1. **Improve layout & spacing**
   - Consistent padding/margins
   - Better responsive breakpoints
   - Card-based layouts

2. **Typography & colors**
   - Establish a color palette
   - Consistent font sizes/weights
   - Better contrast ratios

3. **Component design**
   - Modern card designs for announcements
   - Better buttons (primary/secondary styles)
   - Improved form inputs

### Phase 2: User Experience
1. **Loading states**
   - Skeleton loaders instead of spinners
   - Smooth transitions

2. **Empty states**
   - Helpful messages when no data
   - Call-to-action buttons

3. **Error handling**
   - User-friendly error messages
   - Retry mechanisms

### Phase 3: Advanced Features
1. **Animations**
   - Page transitions
   - Micro-interactions

2. **Accessibility**
   - Keyboard navigation
   - Screen reader support
   - ARIA labels

---

## Recommended UI Libraries (Optional)

If you want to accelerate UI development, consider:

### MudBlazor (Most Popular)
```bash
dotnet add package MudBlazor
```
- Material Design components
- Rich component library (tables, dialogs, cards)
- Built-in theming

### Radzen
```bash
dotnet add package Radzen.Blazor
```
- Great data grids
- Form components
- Professional look

### Blazorise
```bash
dotnet add package Blazorise.Bootstrap5
```
- Bootstrap/Tailwind integration
- Flexible styling

---

## Adding More Mock Data

To add more realistic data to any service, edit:
`SchoolPortal.Client/Services/MockServices.cs`

Example - Adding more announcements:
```csharp
new AnnouncementDto
{
    AnnouncementId = Guid.NewGuid(),
    Title = "Your Title",
    Content = "Your content...",
    Audience = "All",
    CreatedByName = "Author Name",
    CreatedAt = DateTime.UtcNow,
    IsActive = true
}
```

---

## Testing Your UI Changes

1. **Run the app**: `dotnet run` in `SchoolPortal.Client`
2. **Open browser**: Navigate to the displayed localhost URL
3. **Login**: Use any credentials
4. **Test pages**: Navigate and verify your UI changes
5. **Iterate**: Make changes, refresh browser (hot reload enabled)

---

## When the Backend is Ready

Once the Supabase database is ready:

1. Set `"UseMockApi": false` in `appsettings.Development.json`
2. Update `"ApiBaseUrl"` to point to your backend
3. Your UI changes will work with real data
4. Fix any data mismatches (field names, types, etc.)

---

## Current Page Status

| Page | Mock Data | Ready for UI Work |
|------|-----------|-------------------|
| Announcements | ✅ | ✅ |
| Classes | ✅ | ✅ |
| Subjects | ✅ | ✅ |
| Users | ✅ | ✅ |
| Assignments | ⚠️ Partial | ⚠️ |
| Submissions | ✅ | ✅ |
| Grades | ✅ | ✅ |
| Attendance | ✅ | ✅ |

---

## Tips for UI Development

1. **Start with one page** - Perfect the Announcements page first
2. **Create reusable components** - Build a component library as you go
3. **Mobile-first** - Design for mobile, then scale up
4. **Consistent spacing** - Use a spacing scale (4px, 8px, 16px, 24px, 32px)
5. **Test different states** - Empty, loading, error, success
6. **Use browser DevTools** - Inspect responsive layouts

---

## Need Help?

- **Mock data not showing?** Check browser console for errors
- **Changes not reflecting?** Hard refresh (Ctrl+Shift+R)
- **Want to add a new service?** Follow the pattern in `MockServices.cs`

Happy UI development! 🎨
