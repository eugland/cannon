using Cannon.CannonControl;
using NUnit.Framework;
using UnityEngine;

namespace Cannon.Tests.EditMode
{
    public class ChargeModelTests
    {
        private static ChargeSettings Settings => new ChargeSettings
        {
            ChargeTime = 1.2f,
            MinForce = 2.5f,
            MaxForce = 10f
        };

        [Test]
        public void Tap_YieldsMinForce()
        {
            Assert.AreEqual(2.5f, ChargeModel.ForceForHold(0f, Settings), 1e-4f);
        }

        [Test]
        public void FullHold_YieldsMaxForce()
        {
            Assert.AreEqual(10f, ChargeModel.ForceForHold(1.2f, Settings), 1e-4f);
        }

        [Test]
        public void OverHold_ClampsToMaxForce()
        {
            Assert.AreEqual(10f, ChargeModel.ForceForHold(5f, Settings), 1e-4f);
        }

        [Test]
        public void HalfCharge_IsLinearMidpoint()
        {
            float f = ChargeModel.ForceForHold(0.6f, Settings); // half of 1.2s
            Assert.AreEqual(Mathf.Lerp(2.5f, 10f, 0.5f), f, 1e-4f);
        }

        [Test]
        public void ChargeFraction_ClampedToUnitRange()
        {
            Assert.AreEqual(0f, ChargeModel.ChargeFraction(0f, Settings), 1e-4f);
            Assert.AreEqual(0.5f, ChargeModel.ChargeFraction(0.6f, Settings), 1e-4f);
            Assert.AreEqual(1f, ChargeModel.ChargeFraction(99f, Settings), 1e-4f);
        }

        [Test]
        public void ShouldAutoFire_OnlyAtOrPastChargeTime()
        {
            Assert.IsFalse(ChargeModel.ShouldAutoFire(1.19f, Settings));
            Assert.IsTrue(ChargeModel.ShouldAutoFire(1.2f, Settings));
            Assert.IsTrue(ChargeModel.ShouldAutoFire(2f, Settings));
        }

        [Test]
        public void LaunchVelocity_ScalesNormalizedDirectionByForce()
        {
            Vector3 v = ChargeModel.LaunchVelocity(new Vector3(3f, 0f, 0f), 1.2f, Settings);
            Assert.AreEqual(new Vector3(10f, 0f, 0f), v);
        }

        [Test]
        public void LaunchVelocity_ZeroDirection_IsZero()
        {
            Vector3 v = ChargeModel.LaunchVelocity(Vector3.zero, 1.2f, Settings);
            Assert.AreEqual(Vector3.zero, v);
        }
    }
}
