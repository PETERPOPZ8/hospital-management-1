using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HospitalAppointmentSystem.Models;

namespace HospitalAppointmentSystem.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(ApplicationDbContext context)
        {
            await context.Database.EnsureCreatedAsync();

            var passwordHasher = new PasswordHasher<ApplicationUser>();

            // 1. Seed Departments
            if (!context.Departments.Any())
            {
                var departments = new[]
                {
                    new Department
                    {
                        DepartmentName = "Cardiology",
                        Description = "Expert care for heart, blood vessels, and cardiovascular systems.",
                        IconClass = "bi-heart-pulse-fill",
                        IsActive = true
                    },
                    new Department
                    {
                        DepartmentName = "Neurology",
                        Description = "Advanced diagnosis and treatment for brain and nervous system disorders.",
                        IconClass = "bi-cpu-fill",
                        IsActive = true
                    },
                    new Department
                    {
                        DepartmentName = "Pediatrics",
                        Description = "Comprehensive, gentle healthcare services for infants, children, and teens.",
                        IconClass = "bi-emoji-smile-fill",
                        IsActive = true
                    },
                    new Department
                    {
                        DepartmentName = "Orthopedics",
                        Description = "Specialized surgical and non-surgical care for bones, joints, and spine.",
                        IconClass = "bi-activity",
                        IsActive = true
                    }
                };

                await context.Departments.AddRangeAsync(departments);
                await context.SaveChangesAsync();
            }

            var cardoDept = await context.Departments.FirstOrDefaultAsync(d => d.DepartmentName == "Cardiology");
            var neuroDept = await context.Departments.FirstOrDefaultAsync(d => d.DepartmentName == "Neurology");
            var pediaDept = await context.Departments.FirstOrDefaultAsync(d => d.DepartmentName == "Pediatrics");
            var orthoDept = await context.Departments.FirstOrDefaultAsync(d => d.DepartmentName == "Orthopedics");

            // 2. Seed Admin User
            if (!context.Users.Any(u => u.Role == "Admin"))
            {
                var adminUser = new ApplicationUser
                {
                    FullName = "System Administrator",
                    Email = "admin@hospital.com",
                    Phone = "+1 (555) 019-2831",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Admin@123");

                await context.Users.AddAsync(adminUser);
                await context.SaveChangesAsync();
            }

            // 3. Seed Doctors
            if (!context.Doctors.Any())
            {
                var doctorSeeds = new[]
                {
                    new
                    {
                        Name = "Dr. Sarah Jenkins",
                        Email = "sarah.jenkins@hospital.com",
                        Phone = "+1 (555) 234-5678",
                        DeptId = cardoDept?.DepartmentId ?? 1,
                        Qual = "MD, FACC - Harvard Medical School",
                        Spec = "Interventional Cardiology & Electrophysiology",
                        Exp = 14,
                        Fee = 150.00m,
                        Bio = "Dr. Sarah Jenkins is a board-certified Senior Cardiologist with over 14 years of clinical experience in non-invasive imaging, cardiac rehabilitation, and complex arrhythmia management.",
                        Image = "doctor1.jpg"
                    },
                    new
                    {
                        Name = "Dr. Michael Chen",
                        Email = "michael.chen@hospital.com",
                        Phone = "+1 (555) 345-6789",
                        DeptId = neuroDept?.DepartmentId ?? 2,
                        Qual = "MBBS, MD, DM (Neurology) - Johns Hopkins",
                        Spec = "Clinical Neurophysiology & Stroke Care",
                        Exp = 12,
                        Fee = 160.00m,
                        Bio = "Dr. Michael Chen specializes in headache treatment, movement disorders, neuro-rehabilitation, and acute stroke intervention using state-of-the-art diagnostic protocols.",
                        Image = "doctor2.jpg"
                    },
                    new
                    {
                        Name = "Dr. Emily Rodriguez",
                        Email = "emily.rodriguez@hospital.com",
                        Phone = "+1 (555) 456-7890",
                        DeptId = pediaDept?.DepartmentId ?? 3,
                        Qual = "MD (Pediatrics), FAAP - Stanford Health",
                        Spec = "General Pediatrics & Neonatal Care",
                        Exp = 9,
                        Fee = 120.00m,
                        Bio = "Dr. Emily Rodriguez provides compassionate pediatric care focused on child development, preventive wellness checkups, childhood immunizations, and chronic illness management.",
                        Image = "doctor3.jpg"
                    },
                    new
                    {
                        Name = "Dr. Robert Taylor",
                        Email = "robert.taylor@hospital.com",
                        Phone = "+1 (555) 567-8901",
                        DeptId = orthoDept?.DepartmentId ?? 4,
                        Qual = "MS (Orthopedics), MCh - Mayo Clinic",
                        Spec = "Joint Replacement & Sports Medicine",
                        Exp = 16,
                        Fee = 180.00m,
                        Bio = "Dr. Robert Taylor is a renowned orthopedic surgeon specializing in knee/hip total joint replacement, arthroscopic surgery, sports injury rehab, and spine wellness.",
                        Image = "doctor4.jpg"
                    },
                    new
                    {
                        Name = "Dr. Amanda Patel",
                        Email = "amanda.patel@hospital.com",
                        Phone = "+1 (555) 678-9012",
                        DeptId = cardoDept?.DepartmentId ?? 1,
                        Qual = "MD, FESC - Columbia University",
                        Spec = "Heart Failure & Preventative Cardiology",
                        Exp = 8,
                        Fee = 140.00m,
                        Bio = "Dr. Amanda Patel focuses on early risk detection, blood pressure optimization, cholesterol management, and personalized lifestyle cardiotherapy.",
                        Image = "doctor5.jpg"
                    }
                };

                foreach (var doc in doctorSeeds)
                {
                    var user = new ApplicationUser
                    {
                        FullName = doc.Name,
                        Email = doc.Email,
                        Phone = doc.Phone,
                        Role = "Doctor",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    user.PasswordHash = passwordHasher.HashPassword(user, "Doctor@123");

                    await context.Users.AddAsync(user);
                    await context.SaveChangesAsync();

                    var doctor = new Doctor
                    {
                        UserId = user.UserId,
                        DepartmentId = doc.DeptId,
                        Qualification = doc.Qual,
                        Specialization = doc.Spec,
                        Experience = doc.Exp,
                        ConsultationFee = doc.Fee,
                        Biography = doc.Bio,
                        ProfileImage = doc.Image,
                        IsActive = true
                    };
                    await context.Doctors.AddAsync(doctor);
                    await context.SaveChangesAsync();

                    // Seed Availabilities for next 14 days
                    var today = DateTime.Today;
                    for (int i = 0; i < 14; i++)
                    {
                        var availDate = today.AddDays(i);
                        // Skip Sundays
                        if (availDate.DayOfWeek == DayOfWeek.Sunday) continue;

                        var availability = new DoctorAvailability
                        {
                            DoctorId = doctor.DoctorId,
                            AvailableDate = availDate,
                            StartTime = new TimeSpan(9, 0, 0), // 09:00 AM
                            EndTime = new TimeSpan(17, 0, 0),  // 05:00 PM
                            SlotDuration = 30,
                            IsAvailable = true
                        };
                        await context.DoctorAvailabilities.AddAsync(availability);
                    }
                    await context.SaveChangesAsync();
                }
            }

            // 4. Seed Patients
            if (!context.Patients.Any())
            {
                var patientSeeds = new[]
                {
                    new
                    {
                        Name = "John Doe",
                        Email = "patient1@gmail.com",
                        Phone = "+1 (555) 789-0123",
                        DOB = new DateTime(1990, 5, 15),
                        Gender = "Male",
                        Address = "742 Evergreen Terrace, Springfield",
                        Emergency = "+1 (555) 999-1111"
                    },
                    new
                    {
                        Name = "Jane Smith",
                        Email = "patient2@gmail.com",
                        Phone = "+1 (555) 890-1234",
                        DOB = new DateTime(1995, 11, 22),
                        Gender = "Female",
                        Address = "100 Baker Street, London",
                        Emergency = "+1 (555) 999-2222"
                    }
                };

                foreach (var pat in patientSeeds)
                {
                    var user = new ApplicationUser
                    {
                        FullName = pat.Name,
                        Email = pat.Email,
                        Phone = pat.Phone,
                        Role = "Patient",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    user.PasswordHash = passwordHasher.HashPassword(user, "Patient@123");

                    await context.Users.AddAsync(user);
                    await context.SaveChangesAsync();

                    var patient = new Patient
                    {
                        UserId = user.UserId,
                        DateOfBirth = pat.DOB,
                        Gender = pat.Gender,
                        Address = pat.Address,
                        EmergencyContact = pat.Emergency
                    };
                    await context.Patients.AddAsync(patient);
                    await context.SaveChangesAsync();
                }
            }

            // 5. Seed Sample Appointments
            if (!context.Appointments.Any())
            {
                var firstPatient = await context.Patients.FirstOrDefaultAsync();
                var firstDoctor = await context.Doctors.FirstOrDefaultAsync();
                var secondDoctor = await context.Doctors.Skip(1).FirstOrDefaultAsync();

                if (firstPatient != null && firstDoctor != null)
                {
                    var appt1 = new Appointment
                    {
                        PatientId = firstPatient.PatientId,
                        DoctorId = firstDoctor.DoctorId,
                        AppointmentDate = DateTime.Today.AddDays(1),
                        StartTime = new TimeSpan(10, 0, 0),
                        EndTime = new TimeSpan(10, 30, 0),
                        Reason = "Routine cardiac checkup and blood pressure evaluation.",
                        Status = "Confirmed",
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    };
                    await context.Appointments.AddAsync(appt1);

                    if (secondDoctor != null)
                    {
                        var appt2 = new Appointment
                        {
                            PatientId = firstPatient.PatientId,
                            DoctorId = secondDoctor.DoctorId,
                            AppointmentDate = DateTime.Today.AddDays(3),
                            StartTime = new TimeSpan(14, 0, 0),
                            EndTime = new TimeSpan(14, 30, 0),
                            Reason = "Persistent migraine headache consultation.",
                            Status = "Pending",
                            CreatedAt = DateTime.UtcNow.AddDays(-1)
                        };
                        await context.Appointments.AddAsync(appt2);
                    }

                    await context.SaveChangesAsync();
                }
            }
        }
    }
}
